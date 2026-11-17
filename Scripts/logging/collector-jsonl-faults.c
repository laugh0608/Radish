#include <assert.h>
#include <errno.h>
#include <fcntl.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <unistd.h>

static int write_budget = -1;
static int interrupt_write;
static int fail_truncate;
static int fail_close;

static ssize_t test_write(int fd, const void *data, size_t size)
{
    ssize_t count;

    if (interrupt_write) {
        interrupt_write = 0;
        errno = EINTR;
        return -1;
    }
    if (write_budget == 0) {
        errno = ENOSPC;
        return -1;
    }
    if (write_budget < 0) {
        return write(fd, data, size);
    }

    count = write(fd, data, size < (size_t) write_budget ? size : (size_t) write_budget);
    if (count > 0) {
        write_budget -= (int) count;
    }

    return count;
}

static int test_ftruncate(int fd, off_t size)
{
    if (fail_truncate) {
        errno = EIO;
        return -1;
    }

    return ftruncate(fd, size);
}

static int test_close(int fd)
{
    int rc = close(fd);

    if (fail_close && rc == 0) {
        errno = EIO;
        return -1;
    }

    return rc;
}

#define write test_write
#define ftruncate test_ftruncate
#define close test_close
#include "radish_jsonl.c"
#undef close
#undef ftruncate
#undef write

static void replace_file(const char *path, const char *data)
{
    int fd = open(path, O_WRONLY | O_TRUNC);
    size_t size = strlen(data);

    assert(fd >= 0);
    assert(write(fd, data, size) == (ssize_t) size);
    assert(close(fd) == 0);
}

static void assert_contents(const char *path, const char *data)
{
    char content[128];
    int fd = open(path, O_RDONLY);
    size_t size = strlen(data);

    assert(fd >= 0);
    assert(read(fd, content, sizeof(content)) == (ssize_t) size);
    assert(memcmp(content, data, size) == 0);
    assert(close(fd) == 0);
}

int main(void)
{
    struct stat file;
    char path[] = "runtime-jsonl-XXXXXX";
    char overlong[RADISH_JSONL_LINE_LIMIT + 2];
    const char *first = "{\"eventId\":1}\n";
    const char *second = "{\"eventId\":2}\n";
    int fd = mkstemp(path);

    assert(fd >= 0 && close(fd) == 0);
    replace_file(path, first);
    fd = radish_jsonl_open(path, &file);
    assert(fd >= 0 && radish_jsonl_repair_tail(fd, &file) == 0);
    assert(file.st_size == (off_t) strlen(first));
    assert(radish_jsonl_write(fd, second, strlen(second)) == 0);
    interrupt_write = 1;
    write_budget = 4;
    assert(radish_jsonl_write(fd, second, strlen(second)) == -1 && errno == ENOSPC);
    assert(radish_jsonl_finish(fd, file.st_size, 1) == RADISH_JSONL_ROLLED_BACK);
    assert_contents(path, first);
    puts("PASS short write followed by ENOSPC preserves the acknowledged prefix");

    write_budget = -1;
    fd = radish_jsonl_open(path, &file);
    assert(fd >= 0 && radish_jsonl_write(fd, second, strlen(second)) == 0);
    assert(radish_jsonl_finish(fd, file.st_size, 0) == RADISH_JSONL_COMPLETE);
    assert_contents(path, "{\"eventId\":1}\n{\"eventId\":2}\n");
    puts("PASS replay appends exactly one intact record");

    replace_file(path, "{\"eventId\":1}\n{\"old_tail\":");
    fd = radish_jsonl_open(path, &file);
    assert(fd >= 0 && radish_jsonl_repair_tail(fd, &file) == 0);
    assert(file.st_size == (off_t) strlen(first));
    assert(radish_jsonl_write(fd, second, strlen(second)) == 0);
    assert(radish_jsonl_finish(fd, file.st_size, 0) == RADISH_JSONL_COMPLETE);
    assert_contents(path, "{\"eventId\":1}\n{\"eventId\":2}\n");
    puts("PASS a restarted writer repairs the incomplete tail before append");

    memset(overlong, 'x', sizeof(overlong) - 1);
    overlong[sizeof(overlong) - 1] = '\0';
    replace_file(path, overlong);
    fd = radish_jsonl_open(path, &file);
    assert(fd >= 0 && radish_jsonl_repair_tail(fd, &file) == -1);
    assert(file.st_size == (off_t) strlen(overlong) && close(fd) == 0);
    assert(stat(path, &file) == 0 && file.st_size == (off_t) strlen(overlong));
    puts("PASS an unverifiable file boundary remains intact");

    replace_file(path, first);
    fd = radish_jsonl_open(path, &file);
    assert(fd >= 0 && radish_jsonl_write(fd, second, 4) == 0);
    fail_truncate = 1;
    assert(radish_jsonl_finish(fd, file.st_size, 1) == RADISH_JSONL_UNKNOWN);
    assert_contents(path, "{\"eventId\":1}\n{\"ev");
    fail_truncate = 0;
    puts("PASS failed rollback is an unknown outcome");

    replace_file(path, first);
    fd = radish_jsonl_open(path, &file);
    assert(fd >= 0 && radish_jsonl_write(fd, second, strlen(second)) == 0);
    fail_close = 1;
    assert(radish_jsonl_finish(fd, file.st_size, 0) == RADISH_JSONL_UNKNOWN);
    assert_contents(path, "{\"eventId\":1}\n{\"eventId\":2}\n");
    puts("PASS failed close leaves a potentially written batch for diagnosis");

    assert(unlink(path) == 0);
    return 0;
}
