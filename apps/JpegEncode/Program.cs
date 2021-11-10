﻿using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.IO;
using System.Threading.Tasks;

namespace JpegEncode
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var builder = new CommandLineBuilder();

            SetupEncodeCommand(builder.Command);

            builder.UseVersionOption();

            builder.UseHelp();
            builder.UseSuggestDirective();
            builder.RegisterWithDotnetSuggest();
            builder.UseParseErrorReporting();
            builder.UseExceptionHandler();

            Parser parser = builder.Build();
            await parser.InvokeAsync(args);
        }

        static void SetupEncodeCommand(Command command)
        {
            command.Description = "Encode JPEG image.";

            command.AddOption(Output());
            command.AddOption(Quality());
            command.AddOption(OptimizeCoding());

            command.AddArgument(new Argument<FileInfo>()
            {
                Name = "source",
                Description = "The file to encode. (BMP, PNG, JPG file)",
                Arity = ArgumentArity.ExactlyOne
            }.ExistingOnly());

            command.Handler = CommandHandler.Create<FileInfo, FileInfo, int, bool>(EncodeAction.Encode);

            static Option Output() =>
                new Option<string>(new[] { "--output", "--out", "-o" }, "Output JPEG file path.")
                {
                    Name = "output",
                    Arity = ArgumentArity.ExactlyOne
                };

            static Option Quality() =>
                new Option<int>(new[] { "--quality" }, () => 75, "Output JPEG quality.")
                {
                    Name = "quality",
                    Arity = ArgumentArity.ExactlyOne
                };

            static Option OptimizeCoding() =>
                new Option<bool>(new[] { "--quality" }, "Output JPEG quality.")
                {
                    Name = "optimizeCoding",
                };
        }
    }
}
