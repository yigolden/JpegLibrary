using System;
using System.Reflection;
using BenchmarkDotNet.Running;

namespace JpegLibrary.Benchmarks
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            new BenchmarkSwitcher(typeof(Program).GetTypeInfo().Assembly).Run(args);
        }
    }
}
