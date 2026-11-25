FROM cr.fluentbit.io/fluent/fluent-bit:5.1.2@sha256:d792375ca8e53be72fc25716c28f291f32c6fc6f4f31d12d0d14bc78cefe9226 AS upstream

FROM debian@sha256:a2ca65377c251208de42e1a2722cb949211236c161b634f6f3663754e2d06850 AS build
ENV SOURCE_DATE_EPOCH=1788551275
COPY --from=upstream /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/ca-certificates.crt
RUN printf '%s\n' \
      'deb [check-valid-until=no signed-by=/usr/share/keyrings/debian-archive-keyring.gpg] https://snapshot.debian.org/archive/debian/20261005T000000Z/ trixie main' \
      'deb [check-valid-until=no signed-by=/usr/share/keyrings/debian-archive-keyring.gpg] https://snapshot.debian.org/archive/debian-security/20261005T000000Z/ trixie-security main' > /etc/apt/sources.list && \
    rm -f /etc/apt/sources.list.d/debian.sources && \
    apt-get -o Acquire::https::CaInfo=/etc/ssl/certs/ca-certificates.crt -o APT::Update::Error-Mode=any update && \
    apt-get -o Acquire::https::CaInfo=/etc/ssl/certs/ca-certificates.crt install -y --no-install-recommends bison build-essential cmake flex pkg-config libssl-dev zlib1g-dev && \
    rm -rf /var/lib/apt/lists/*

COPY source /source
RUN cmake -S /source -B /build -DCMAKE_BUILD_TYPE=Release \
      -DFLB_RELEASE=On -DFLB_EXAMPLES=Off -DFLB_SHARED_LIB=Off \
      -DFLB_HTTP_SERVER=On -DFLB_METRICS=On -DFLB_LUAJIT=On \
      -DFLB_CONFIG_YAML=Off -DFLB_IN_SYSTEMD=Off -DFLB_OUT_PGSQL=Off \
      -DFLB_KAFKA=Off -DFLB_OUT_KAFKA_REST=Off -DFLB_WASM=Off && \
    cmake --build /build --target fluent-bit-bin --parallel 4

FROM upstream
LABEL org.opencontainers.image.revision="66910c10a4d7eafa810b84229cf2dcf7b0f26f97" \
      radish.logging.candidate="storage-errors"
COPY --from=build /build/bin/fluent-bit /fluent-bit/bin/fluent-bit
RUN ["/fluent-bit/bin/fluent-bit", "--version"]
