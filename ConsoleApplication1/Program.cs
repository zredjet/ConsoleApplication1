namespace ConsoleApplication1
{
    #region using

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Symbols;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Text;
    using Microsoft.CodeAnalysis.MSBuild;
    using System.Threading.Tasks;
    using CsvHelper;
    using System.IO;
    using CommandLine;
    using CommandLine.Text;

    #endregion

    public static class Program
    {
        private static void Main(string[] args)
        {
            var da = new DocumentAnalyzer(args);
            da.Execute();
        }
    }

    public class DocumentAnalyzer
    {
        private ParserResult<CommandLineOptions> _parserResult;
        private CommandLineOptions _options;

        public DocumentAnalyzer(string[] args)
        {
            _parserResult = Parser.Default.ParseArguments<CommandLineOptions>(args);
        }

        public void Execute()
        {
            _parserResult.WithParsed(options => _options = options);
            if (ValidateOptions())
            {
                var aaa = this.Execute1Async();
            }
            else
            {
                Console.WriteLine("処理失敗\r\n" +
                    " - オプションの指定に誤りがあります。\r\n" +
                    " - ”-help” で確認してください。");
            }
        }

        public bool ValidateOptions()
        {
            //
            if (_options == null)
            {
                Console.WriteLine("引数が指定されていません。");
                return false;
            }

            //
            if (!String.IsNullOrEmpty(_options.SolutionPath))
            {
                if (!File.Exists(_options.SolutionPath))
                {
                    Console.WriteLine("ソリューションファイルが存在しません。");
                    return false;
                }
            }

            //
            if (!String.IsNullOrEmpty(_options.CSProjectPath))
            {
                if (!File.Exists(_options.CSProjectPath))
                {
                    Console.WriteLine("プロジェクトファイルが存在しません。");
                    return false;
                }
            }

            //
            if (String.IsNullOrEmpty(_options.SolutionPath) && String.IsNullOrEmpty(_options.CSProjectPath))
            {
                Console.WriteLine("ソリューションファイルか、プロジェクトファイルのどちらかを指定してください。");
            }

            return true;
        }

        public async Task<bool> Execute1Async()
        {
            var csvAR = new CsvAnalyzerResult();
            csvAR.CsvFilePath = _options.OutputPath;

            if (File.Exists(_options.OutputPath))
            {
                // 前回分
                var en = csvAR.ReadAll<AnalyzeResult>();
                var tempList = en.Any() ? en.ToList() : new List<AnalyzeResult>();
                csvAR.CloseReader();
                // 追加
                csvAR.InitializeCsvWriter();
                csvAR.InitializeHeader<AnalyzeResult>();
                csvAR.WriteList<AnalyzeResult>(tempList);
            }
            else
            {
                csvAR.InitializeCsvWriter();
                csvAR.InitializeHeader<AnalyzeResult>();
            }

            //
            var aList = new List<AnalyzeResult>();

            //ソリューションファイル
            if (!String.IsNullOrEmpty(_options.SolutionPath))
            {
                var msWorkspace = MSBuildWorkspace.Create();
                var solution = msWorkspace.OpenSolutionAsync(_options.SolutionPath).Result;

                foreach (var project in solution.Projects)
                {
                    var tempList = GetSpecifiedIdentifierNameResult(project).Result;
                    if (tempList.Count != 0) aList.AddRange(tempList);
                }
            }

            // プロジェクトファイル
            if (!String.IsNullOrEmpty(_options.CSProjectPath))
            {
                var msWorkspace = MSBuildWorkspace.Create();
                var project = msWorkspace.OpenProjectAsync(_options.CSProjectPath).Result;

                var tempList = GetSpecifiedIdentifierNameResult(project).Result;
                if (tempList.Count != 0) aList.AddRange(tempList);
            }

            // CSVへ書き込み。
            csvAR.WriteList<AnalyzeResult>(aList);
            csvAR.CloseWriter();

            return true;
        }

        public async Task<List<AnalyzeResult>> GetSpecifiedIdentifierNameResult(Microsoft.CodeAnalysis.Project project)
        {
            var aList = new List<AnalyzeResult>();

            foreach (var document in project.Documents)
            {
                if (_options.Verbose)
                    Console.WriteLine("■" + project.Name + "\t\t\t" + document.Name);

                // ただの同期取得。
                var tree2 = document.GetSyntaxTreeAsync().Result;
                var model2 = document.GetSemanticModelAsync().Result;
                //var root2 = await document.GetSyntaxRootAsync().ConfigureAwait(false);

                var identifierName = tree2.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>();
                foreach (var n in identifierName)
                {
                    if (_options.Verbose)
                    {
                        Console.WriteLine(" * IdentifierNameSyntax * " + n.ToString());
                        Console.WriteLine("\t" + "TYPE   - " + model2.GetTypeInfo(n).Type);
                        Console.WriteLine("\t" + "SYMBOL - " + model2.GetSymbolInfo(n).Symbol.ContainingType?.ToString());
                    }

                    var ts = model2.GetTypeInfo(n).Type?.ToString();
                    var ts2 = model2.GetSymbolInfo(n).Symbol.ContainingType?.ToString();

                    string typeString = ts;
                    string symbolString = ts2;
                    string targetString = "";
                    foreach (var nameSpace in _options.FindNameSpace)
                    {
                        if (String.IsNullOrEmpty(ts) || !ts.Contains(nameSpace) || !ts.StartsWith(nameSpace))
                        {
                            if (String.IsNullOrEmpty(ts2) || !ts2.Contains(nameSpace) || !ts2.StartsWith(nameSpace))
                            {
                                continue;
                            }
                        }

                        if (!String.IsNullOrEmpty(symbolString) && symbolString.Contains(nameSpace))
                        {
                            targetString = symbolString + "." + n.ToString();
                        }
                        else
                        {
                            targetString = typeString;
                        }

                        var ar = new AnalyzeResult
                        {
                            ProjectName = project.Name,
                            SrcFilePath = document.FilePath,
                            LineNumberSt = tree2.GetLineSpan(n.FullSpan).StartLinePosition.Line,
                            LineNumberEd = tree2.GetLineSpan(n.FullSpan).EndLinePosition.Line,
                            SymbolString = symbolString,
                            TypeString = typeString,
                            TargetString = targetString,
                            IsMethod = model2.GetTypeInfo(n).Type == null ? "*" : "",
                            Name = n.ToString(),
                            RawString = n.Parent.ToString(),
                        };

                        aList.Add(ar);
                    }
                }
            }

            return aList;
        }
    }

    public class AnalyzeResult
    {
        #region プロパティ
        public string ProjectName { get; set; }
        public string SrcFilePath { get; set; }
        public int LineNumberSt { get; set; }
        public int LineNumberEd { get; set; }
        public string SymbolString { get; set; }
        public string TypeString { get; set; }
        public string TargetString { get; set; }
        public string IsMethod { get; set; }
        public string Name { get; set; }
        public string RawString { get; set; }
        #endregion

        #region コンストラクタ
        public AnalyzeResult() {; }
        #endregion
    }

    public class CsvAnalyzerResult
    {
        #region プロパティ
        public String CsvFilePath { get => this._csvFilePath; set => this._csvFilePath = value; }
        #endregion

        #region フィールド
        private TextReader _textFileReader { get; set; }
        public TextWriter _textFileWriter { get; set; }
        public CsvReader _csvAnalyzerResultReader { get; set; }
        public CsvWriter _csvAnalyzerResultWriter { get; set; }
        private String _csvFilePath = "";
        #endregion

        #region コンストラクタ
        public CsvAnalyzerResult() { ; }
        #endregion

        #region メソッド
        public void InitializeCsvWriter() { _textFileWriter = new StreamWriter(_csvFilePath); _csvAnalyzerResultWriter = new CsvWriter(_textFileWriter); }
        public void CloseWriter()
        {
            _csvAnalyzerResultWriter.Flush();
            _csvAnalyzerResultWriter.Dispose();
            _csvAnalyzerResultWriter = null;
            _textFileWriter.Close();
            _textFileWriter = null;
        }
        public void WriteList<T>(List<T> list) { _csvAnalyzerResultWriter.WriteRecords(list); }
        public void InitializeHeader<T>() { _csvAnalyzerResultWriter.WriteHeader<T>(); _csvAnalyzerResultWriter.NextRecord(); }
        public void Flush() { _csvAnalyzerResultWriter.Flush(); }
        public IEnumerable<T> ReadAll<T>()
        {
            try
            {
                if (!File.Exists(_csvFilePath)) return null;
                _textFileReader = new StreamReader(_csvFilePath);
                _csvAnalyzerResultReader = new CsvReader(_textFileReader);
                return _csvAnalyzerResultReader.GetRecords<T>();
            }
            finally
            {
            }
        }
        public void CloseReader() { _csvAnalyzerResultReader.Dispose(); _csvAnalyzerResultReader = null; _textFileReader.Close(); _textFileReader = null; }
        #endregion
    }

    public class CommandLineOptions
    {
        [Option('s', "sln",
            Default = "",
            HelpText = "ソリューションファイルのパスを指定してください。")]
        public string SolutionPath { get; set; }

        [Option('c', "csproj",
            Default = "",
            HelpText = "プロジェクトファイルのパスを指定してください。")]
        public string CSProjectPath { get; set; }

        [Option('v', "vervose", HelpText = "デバッグ情報を出力したい場合に指定してください。")]
        public bool Verbose { get; set; }

        [Option('o', "output",
            Default = "temp.csv",
            HelpText = "出力するファイル名を指定してください。")]
        public string OutputPath { get; set; }

        [Value(0,
            Required = true,
            Default = "",
            HelpText = "対象とする名前空間を指定してください。")]
        public IEnumerable<string> FindNameSpace { get; set; }

    }
}
