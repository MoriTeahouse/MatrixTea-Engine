# MatrixTea ATR1

ATR1 is an engine-owned packaging format. Use the MatrixTea packaging module or the Artelu launcher to read it. Ordinary ZIP tools cannot open the outer container.

`dotnet run --project src/MatrixTea.Packaging.Cli -- pack <game-folder> <output.atr>`

`dotnet run --project src/MatrixTea.Packaging.Cli -- unpack <input.atr> <empty-folder>`

The packaging module and CLI require .NET 8; existing Core/MonoGame engine modules still target .NET 6. The launcher ships the packaging module and its own runtime, so players do not need to install developer tools.

## Algorithms and layout

The payload is a ZIP index with uncompressed file data. Its byte stream is split into at most 1 MiB blocks. Each block is encoded using the smallest result of Brotli, Deflate, or raw bytes. AES-256-GCM encrypts and authenticates the selected bytes with a 128-bit tag. A random 128-bit archive salt derives a fresh key through HMAC-SHA-256; the nonce includes the sequential frame index. The header and frame metadata are authenticated as associated data. An authenticated final frame verifies total length and rejects truncation or appended data. Release manifests additionally carry SHA-256 for the complete download.

Header: 16-byte `ATR1MATRIXTEA` magic padded with zeros, 16-byte salt, 8-byte little-endian payload length. Frame: 1-byte codec (0 raw, 1 Brotli, 2 Deflate, 255 end), 4-byte original length, 4-byte encoded length, 4-byte index, 16-byte GCM tag, then encrypted bytes. Maximum payload/extracted bytes: 4 GiB; maximum files: 20,000. Readers reject unsafe Windows names, escaping paths, duplicate paths, symbolic links, oversized blocks and incomplete files.

## Security boundary

The engine format key is distributed in the reader. It is deliberately not a private signing key. Anyone with the engine/source can implement another reader or recover that key. AES-GCM detects corruption but does not prove publisher identity against somebody who knows the format key. ATR is not DRM and cannot guarantee that only our executable can ever extract it. Trusted distribution relies on the configured GitHub HTTPS release source; hashes do not replace publisher signatures. Never put passwords, credentials or other secrets inside packages on the assumption that ATR hides them permanently.

Extraction requires an empty actual directory. For installation, use a temporary staging directory and activate it only after extraction and runtime verification succeed. The caller owns cleanup on extraction failure; Artelu's installer always removes its GUID staging job.
