# Caraota
[![Runtime](https://img.shields.io/badge/.NET-10.0-512bd4)](https://dotnet.microsoft.com/)
![Encryption](https://img.shields.io/badge/Crypto-Shanda%20%2F%20AES-informational?style=flat)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

# **Caraota: Packet Interceptor & Logger for MapleStory v62**

**Caraota** is a network interception engine purpose-built for protocol research on MapleStory private servers (specifically v62). Caraota ensures near-zero latency and absolute stability of the Initialization Vector (IV).

[Check out the Wiki](https://github.com/jmartinimena/Caraota/wiki) for examples.
---

## 🚀 Performance Benchmarks

*Tests performed on: Ryzen 5 5600X 3.7GHz, 32 GB DDR4 3600MT/s*

| Component | Optimization | Impact |
| :--- | :--- | :--- |
| **Cryptography** | Bitwise & Local Registers | ~2,500 ns |
| **Memory Management** | Zero-Allocation (ArrayPool) | 0 B Garbage |
| **IV Validation** | Double-Buffer Logic | Total Stability |
| **Global Pipeline** | **Ultra-Low Latency** | **~70,000 ns** |

---

## 🛠️ Key Features

### 1. Interception & MITM (Man-In-The-Middle)
Caraota leverages **WinDivert** to operate at the Kernel level, allowing not just observation, but real-time interception and modification of network traffic.
* **Packet Hijacking**: Modify payloads (item swaps, chat messages, coordinates) before they reach their destination.
* **Drop & Inject**: Discard legitimate packets and inject custom sequences without desynchronizing the TCP session.
* **Auto-Checksum Correction**: Automatically recalculates IP and TCP checksums following any payload modification.

### 2. Zero-Allocation Engineering
The engine is architected to bypass the Garbage Collector (GC) in the "Hot Path":
* **Spans & Memory**: Buffer processing via `ReadOnlySpan<byte>` to avoid costly copies (`.ToArray()`).
* **Stackalloc**: IV update seeds are managed on the Stack, eliminating Heap pressure.
* **ArrayPool Integration**: Efficient buffer reuse for high-intensity network traffic.

### 3. Advanced v62 Cryptography
Native and optimized implementation of the MapleStory protocol:
* **Custom Shanda**: Redesigned with bit rotation (`ROL`) and local register loading for maximum throughput.
* **Fast Header Generation**: Header generation using `BinaryPrimitives` and bitwise operations, eliminating slow division and modulo operations.

---

## 🔧 Requirements & Installation

1.  **.NET 10** or higher.
2.  **WinDivert**: Ensure `WinDivert.dll` and `WinDivert64.sys` are present in the execution directory.
3.  **Administrator Privileges**: Required for the WinDivert driver to open a handle to the network stack.

```bash
# Clone the repository
git clone [https://github.com/jmartinimena/Caraota.git](https://github.com/jmartinimena/Caraota.git)

# Build in Release mode for maximum performance
dotnet build -c Release
```
