# JpegLibrary

A pure C# implementation of JPEG decoder and encoder.

## Supported Runtimes

* .NET Framework 4.6+
* .NET Core 2.0+
* .NET Standard 2.0+

## Currently Supported Features

* Decode baseline DCT-based JPEG (Huffman coding, SOF0)
* Decode extended sequential DCT-based JPEG (Huffman coding, SOF1)
* Decode progressive DCT-based JPEG (Huffman coding, SOF2)
* Decode lossless JPEG (Huffman coding, SOF3)
* Encode baseline DCT-based JPEG (Huffman coding, SOF0) with optimized coding.
* Optimize an existing SOF0 image to use optimized Huffman coding.
