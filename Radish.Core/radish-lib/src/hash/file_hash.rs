use sha2::{Sha256, Digest};
use std::fs::File;
use std::io::{BufReader, Read};
use anyhow::{Result, Context};

/// Calculate SHA256 hash of a file
pub fn calculate_sha256(file_path: &str) -> Result<String> {
    // Open file
    let file = File::open(file_path)
        .context(format!("Failed to open file: {}", file_path))?;

    let mut reader = BufReader::new(file);
    let mut hasher = Sha256::new();

    // Read file in chunks and update hasher
    let mut buffer = [0u8; 8192]; // 8KB buffer
    loop {
        let bytes_read = reader.read(&mut buffer)
            .context("Failed to read file")?;

        if bytes_read == 0 {
            break;
        }

        hasher.update(&buffer[..bytes_read]);
    }

    // Finalize and get hash
    let result = hasher.finalize();
    let hash_string = format!("{:x}", result);

    Ok(hash_string)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;
    use std::fs;

    #[test]
    fn test_sha256_calculation() {
        // Create a temporary test file
        let test_file = "test_hash.txt";
        let test_content = b"Hello, Radish!";

        {
            let mut file = File::create(test_file).unwrap();
            file.write_all(test_content).unwrap();
        }

        // Calculate hash
        let hash = calculate_sha256(test_file).unwrap();

        // Verify hash is 64 characters (SHA256 in hex)
        assert_eq!(hash.len(), 64);

        // Clean up
        fs::remove_file(test_file).unwrap();
    }
}
