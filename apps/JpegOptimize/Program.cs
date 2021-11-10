﻿using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Threading.Tasks;

namespace JpegOptimize
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var builder = new CommandLineBuilder();

            SetupOptimizeCommand(builder.Command);

            builder.UseVersionOption();

            builder.UseHelp();
            builder.UseSuggestDirective();
            builder.RegisterWithDotnetSuggest();
            builder.UseParseErrorReporting();
            builder.UseExceptionHandler();

            Parser parser = builder.Build();
            await parser.InvokeAsync(args);
        }

        static void SetupOptimizeCommand(Command command)
        {
            command.Description = "Optimize a baseline JPEG image for file size.";

            command.AddOption(Output());

            command.AddArgument(new Argument<FileInfo>()
            {
                Name = "source",
                Description = "The JPEG file to optimize.",
                Arity = ArgumentArity.ExactlyOne
            }.ExistingOnly());


            command.Handler = CommandHandler.Create<FileInfo, FileInfo>(OptimizeAction.Optimize);

            static Option Output() =>
                new Option<string>(new[] { "--output", "--out", "-o" }, "Output optimized JPEG file.")
                {
                    Name = "output",
                    Arity = ArgumentArity.ExactlyOne
                };
        }
    }
}
