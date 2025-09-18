# Compresion\_RAR

> **Academic / Technical README**
>
> A Windows desktop application (WPF) that demonstrates file and folder compression and decompression using classical lossless coding algorithms (Huffman and Shannon–Fano). The project is implemented in C# with a visual front-end and modular services and algorithm components designed for study, experimentation, and small-scale usage.

---

## Abstract

This repository implements a GUI-based compression tool (Compresion\_RAR) that bundles files and folders into a custom archive format and applies lossless entropy coding using **Huffman** and **Shannon–Fano** methods. The project is intended for both educational and experimental use: it provides a complete pipeline from UI interaction to file I/O, archive layout, encryption-hash options (password protection via SHA256-derived key), progress reporting, and background processing with cancellation and pause/resume support.

The codebase is structured to separate concerns: UI (WPF Views), Services (file/folder/archive orchestration), and Algorithms (implementations of Huffman and Shannon–Fano compressors). This README explains the architecture, algorithms, design decisions, build/run instructions, usage, evaluation approaches, and suggestions for future work.

---

## Key Features

* Graphical desktop application (WPF) for compressing/decompressing files and folders.
* Two implemented entropy coding algorithms:

  * **Huffman coding** (variable-length prefix codes based on frequency)
  * **Shannon–Fano coding** (top-down symbol partitioning)
* Archive format that stores metadata (file names, relative paths, extensions) and compressed blocks.
* Support for bundling multiple files into a single archive.
* Optional password protection (hashed using SHA-256) for simple file integrity/obfuscation.
* Asynchronous, cancellable, and pausable operations with a progress reporting UI.
* Modular codebase to allow adding more algorithms (e.g., LZ77, LZ78, DEFLATE) or improving performance.

---

## Academic Motivation and Scope

Lossless compression is critical in systems that require exact reconstruction of original data. Huffman and Shannon–Fano algorithms are canonical textbook methods for entropy coding; implementing them helps illustrate key concepts:

* statistical modeling of symbol frequencies;
* code assignment and prefix properties;
* trade-offs between optimality and implementation simplicity;
* integration challenges when embedding coding into a full archive pipeline (metadata, streaming, block boundaries, error handling).

This project focuses on a clear, readable implementation suitable for teaching and empirical evaluation rather than maximum throughput or extreme compression on very large datasets.

---

## Technology Stack

* Language: **C#**
* UI: **WPF (XAML)**
* Target Framework: **.NET Framework 4.7.2** (project references indicate `net472`)
* Development: Visual Studio (2017/2019/2022 compatible)
* Third-party libraries (NuGet) used by the project (present in `packages.config`): examples include `Accord`, `Magick.NET`, `libzopfli-sharp`, `MaterialDesignThemes`, and other compatibility/system packages. The project restores these NuGet dependencies on build.

---

## Repository / Code Structure (high-level)

Below is a concise, human-readable mapping of the important folders and files (truncated to the most relevant parts):

```
Compresion_RAR.sln
Compresion_RAR.csproj
MainWindow.xaml                # Main WPF window (UI entry point)
MainWindow.xaml.cs

/Views
  ├─ FilesSectionWindow.xaml(.cs)   # File/folder selection and main operations
  ├─ FileSectionWindow.xaml(.cs)    # Single-file operations UI
  ├─ ProgressWindow.xaml(.cs)      # Progress, pause/resume UI
  └─ CompressedItemsListBox.xaml(.cs)

/Algorithms
  ├─ /Huffman
  │   ├─ HuffmanCompressor.cs
  │   └─ HuffmanNode.cs
  └─ /Shannon_Fano  (sometimes named ShannoFano)
      ├─ ShannonFanoCompressor.cs
      └─ Symbol.cs

/Services (Helpers namespace)
  ├─ ArchiveService.cs    # High-level archive format I/O (pack/unpack)
  ├─ FileService.cs       # Single-file compress/decompress orchestration
  └─ FolderService.cs     # Folder traversal and bundling logic

/Enums
  └─ Algorithm.cs         # enum { HUFFMAN, SHANNON_FANO }

packages.config

other project/obj/bin artifacts
```

> Note: filenames above match the project artifacts in the repository. The algorithm implementations are intentionally modular so that the `Services` layer can call either compressor implementation by passing byte arrays and receiving compressed bytes.

---

## Archive Format (Conceptual)

The repository implements a custom, simple archive format to store multiple files and their metadata. Key ideas:

* Archive header contains archive-level metadata (e.g., number of files, optional global password hash if used).
* For each file, the archive records the relative path length and bytes, followed by the compressed data block.
* Each compressed block may be stored with a small header (lengths, algorithm identifier, checksum) so the decompressor can safely restore the file.

This design emphasizes simplicity and learnability. For production-grade archives (reliability, cross-platform compatibility, streaming), consider adopting established formats (ZIP, 7z) or adding extra headers and checksums (CRC32/CRC64) and stronger encryption (e.g., AES-GCM).

---

## Algorithms — Theory and Implementation Notes

### Huffman Coding (files: `Algorithms/Huffman/HuffmanCompressor.cs`, `HuffmanNode.cs`)

**High-level idea:** Build a binary tree by repeatedly merging the two least-frequent symbols; assign bitstrings by taking left/right paths from the root. Short codes for common symbols, long codes for rare ones. The resulting code is prefix-free and optimal for symbol-by-symbol coding when symbol probabilities are known.

**Implementation notes (in this project):**

* Frequency table computed on input byte values (0..255).
* A priority queue or sorted list is used to select the two least frequent nodes and construct the Huffman tree (class `HuffmanNode` provides node structure and comparators).
* The tree is traversed to assign codewords to symbols (map: byte → bitstring).
* The bitstrings are packed into bytes (bit buffer) and written to the archive, plus a serialized code-table so the decoder can reconstruct the same tree.

**Complexity:** Building frequencies O(n) where n is input length; tree build O(k log k) where k ≤ 256 unique symbols; encoding O(n · average\_code\_length).

**Limitations:** Huffman coding is optimal for symbol-by-symbol coding only when you know true symbol probabilities; it does not exploit longer-range redundancy like LZ-family algorithms.

### Shannon–Fano Coding (files: `Algorithms/Shannon_Fano/ShannonFanoCompressor.cs`, `Symbol.cs`)

**High-level idea:** Sort symbols by frequency and recursively split into two groups with approximately equal total frequency; assign one branch the `0` prefix and the other the `1` prefix. Continue until groups are trivial.

**Implementation notes:**

* Frequency counting and sorting is performed first.
* The symbolic partitioning is implemented via recursion to build binary codes for each symbol.
* The encoder writes the code table and the packed bits into the archive similarly to Huffman.

**Complexity:** Sorting costs O(k log k), and the code assignment is O(k·log k) in practice; encoding is O(n·avg\_code\_length).

**Practical notes:** Shannon–Fano sometimes produces suboptimal codes (worse than Huffman) depending on symbol distribution, but is conceptually simpler and instructional.

---

## Build & Run (Windows + Visual Studio)

### Prerequisites

* Windows OS
* Visual Studio 2017/2019/2022 (Community/Professional/Enterprise)
* .NET Framework 4.7.2 developer targeting pack installed (the project references `net472`)
* NuGet package restore enabled (Visual Studio will restore packages on build)

### Steps

1. Extract the repository ZIP to a working directory.
2. Open `Compresion_RAR.sln` in Visual Studio.
3. From the `Build` menu: `Restore NuGet Packages` (or `Build` which will trigger restore automatically).
4. Set the startup project to `Compresion_RAR` and run (F5) or use `Debug → Start Without Debugging`.

**Command-line (optional):**

* Restore and build using `nuget` + `msbuild` (example):

```bash
# from repo root
nuget restore Compresion_RAR.sln
msbuild Compresion_RAR.sln /p:Configuration=Release
```

> If using .NET CLI is required, porting to SDK-style projects and .NET Core / .NET 6+ would be necessary (not provided in the current repository layout).

---

## Running the Application (User Flow)

1. Launch the application (MainWindow).
2. Choose your desired operation: compress a single file, compress multiple files, or compress a folder.
3. Choose the compression algorithm: **Huffman** or **Shannon–Fano**.
4. (Optional) Provide a password — the project uses SHA-256 hashing for password-derived validation; note that this is not authenticated encryption.
5. Start the operation — the progress window shows progress, allows pausing/resuming, and cancellation.
6. Decompress by selecting an archive — the service will read metadata and reconstruct files with relative paths.

> The UI windows of interest: `MainWindow.xaml`, `Views/FilesSectionWindow.xaml`, `Views/FileSectionWindow.xaml`, and `Views/ProgressWindow.xaml`.

---

## Evaluation — How to Measure Performance & Correctness

**Correctness**

* Verify round-trip integrity: compress a file and immediately decompress it; compare byte-for-byte equality with the original. Example: use a hash (SHA-256) before and after.

**Compression Efficiency**

* **Compression ratio** = compressed\_size / original\_size (lower is better).
* Evaluate with datasets of different properties (text, binary executables, images, already-compressed data) to see algorithm behavior.

**Time & Throughput**

* Measure wall-clock time (milliseconds) for compress/decompress operations and compute throughput (bytes/sec).
* Measure memory consumption if profiling is needed.

**Suggested experiments**

* Compare Huffman vs Shannon–Fano on the same dataset and report compression ratio and runtime.
* Compare to standard tools (e.g., `gzip -9`) to put the results in context.

---

## Extending the Project (Research / Development Ideas)

The current codebase is a good scaffold for research or educational improvements:

1. **Add LZ-family algorithms** (LZ77, LZ78, LZW) to capture repeated substrings before entropy coding.
2. **Combine LZ + Huffman** (DEFLATE-like) for far better real-world compression on many file types.
3. **Streaming and block-based compression** for very large files to keep memory bounded.
4. **Stronger encryption**: use established authenticated encryption (AES-GCM) instead of only password hashing for confidentiality and integrity.
5. **Profiling and optimization**: replace naive sorted-lists with an efficient priority queue/binary heap for Huffman tree building; use bit-level buffers with minimal allocations.
6. **Unit tests**: create a test suite (NUnit/xUnit) for algorithm correctness, archive round-trips, and edge cases (empty files, single-byte files, very large files).
7. **Cross-platform port**: port to .NET 6+ with a cross-platform UI framework (Avalonia or MAUI) to run on Linux/macOS.

---

## Troubleshooting

* **NuGet restore errors:** Ensure your machine has internet access and that Visual Studio has the correct package sources. If a package reference cannot be found, check `packages.config` and consider updating or removing obsolete packages.
* **Target framework mismatches:** Install the `.NET Framework 4.7.2 Developer Pack` or retarget to an available framework in Project Properties.
* **Runtime exceptions during compression:** Use the progress and logs printed by the UI to identify which file or block triggered the error; test small files first and verify file permissions.

---

## Reproducibility & Tests (Suggested Commands)

Measure compression ratio and correctness for a file `example.bin` (manual process):

1. Compute original hash (e.g., SHA256) using any hashing tool.
2. Use the GUI to compress `example.bin` with Huffman; record compressed file size and runtime.
3. Decompress and recompute SHA256; compare with original.

For automated measurement, consider writing a small C# console harness (or PowerShell script) that calls the internal services (FileService / ArchiveService) or runs the built executable with test inputs.

---

## References (recommended reading)

* David A. Huffman, *A Method for the Construction of Minimum-Redundancy Codes*, Proceedings of the IRE, 1952.
* C. E. Shannon, *A Mathematical Theory of Communication*, Bell System Technical Journal, 1948 (background on information theory).
* Textbook references on data compression (e.g., Salomon, Sayood) for practical comparisons and theoretical foundations.

---

## License & Attribution

No explicit license file appears in the repository. If you plan to open-source or publish this project, add a license (e.g., **MIT**, **Apache 2.0**) to the repository root and update the README to reflect the chosen license.

---

## Authors / Contributors

* Original project author(s) — see the Git history for actual contributor names.
* This README was generated to document the implementation and to support academic/engineering work on the project.

---

## How you can cite or use this repository

If you use this project in academic work or reports, cite the repository and describe which algorithms and versions you evaluated. Mention any modifications you make (e.g., porting, algorithmic changes) and provide experimental setup details.

---

### Quick Contact / Next Steps

If you want, I can:

* produce a cleaned, minimal `ARCHITECTURE.md` diagram summarizing data flow;
* convert this README into a downloadable `README.md` file placed in the repo root (I can provide the contents or write the file for you);
* extract and document the algorithm pseudocode for classroom handouts from the actual source functions.

---

