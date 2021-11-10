# JpegLibrary

JPEG decoder, encoder and optimizer implemented in C#.

[![Build Status](https://dev.azure.com/jinyi0679/yigolden/_apis/build/status/yigolden.JpegLibrary?branchName=master)](https://dev.azure.com/jinyi0679/yigolden/_build/latest?definitionId=2&branchName=master)

## Supported Runtimes

* .NET Framework 4.6.1+
* .NET Core 3.1+
* Runtimes compatible with .NET Standard 2.0

## Supported Features


### Decode
* Decode Huffman-coding baseline DCT-based JPEG (SOF0)
* Decode Huffman-coding extended sequential DCT-based JPEG (SOF1)
* Decode Huffman-coding progressive DCT-based JPEG (SOF2)
* Decode Huffman-coding lossless JPEG (SOF3)
* Decode arithmetic-coding sequential DCT-based JPEG (SOF9)
* Decode arithmetic-coding progressive DCT-based JPEG (SOF10)

See [JpegDecode](https://github.com/yigolden/JpegLibrary/blob/master/apps/JpegDecode/DecodeAction.cs) program for example.

### Encode
* Encode Huffman-coding baseline DCT-based JPEG (SOF0) with optimized coding.

See [JpegEncode](https://github.com/yigolden/JpegLibrary/blob/master/apps/JpegEncode/EncodeAction.cs) program for example.

### Optimize
* Optimize an existing baseline image to use optimized Huffman coding.

See [JpegOptimize](https://github.com/yigolden/JpegLibrary/blob/master/apps/JpegOptimize/OptimizeAction.cs) program for example.
