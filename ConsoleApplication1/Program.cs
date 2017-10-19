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

    #endregion

    public static class Program
    {
        private static void Main(string[] args)
        {
            //
            var da = new DocumentAnalyzer();
            da.Execute1Async();
        }
    }

    public class DocumentAnalyzer
    {
        //string solutionPath = @"C:\Users\redjet\source\repos\zip-test-stellaris\zip-test-stellaris.sln";
        string solutionPath = @"C:\Users\redjet\source\repos\WindowsFormsApp1\WindowsFormsApp1.sln";

        public async void Execute1Async()
        {
            var csvFilePath = @"aaa.csv";
            var csvAR = new CsvAnalyzerResult();
            csvAR.SetFilePath(csvFilePath);
            if (File.Exists(csvFilePath))
            {
                //
                var en = csvAR.ReadAll<AnalyzeResult>();
                var tempList = en.Any() ? en.ToList() : new List<AnalyzeResult>();
                csvAR.CloseReader();
                //
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

            //
            var msWorkspace = MSBuildWorkspace.Create();
            var solution = msWorkspace.OpenSolutionAsync(solutionPath).Result;
            foreach (var project in solution.Projects)
            {
                foreach (var document in project.Documents)
                {
                    //Console.WriteLine("■" + project.Name + "\t\t\t" + document.Name);

                    var tree1 = document.GetSyntaxTreeAsync();
                    tree1.Wait();
                    var tree2 = await tree1;

                    var model1 = document.GetSemanticModelAsync();
                    model1.Wait();
                    var model2 = await model1;

                    var root1 = document.GetSyntaxRootAsync();
                    root1.Wait();
                    var root2 = await root1;

                    var simpleAccess = tree2.GetRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>();
                    foreach (var n in simpleAccess)
                    {
                        var ts = model2.GetTypeInfo(n.Expression).Type?.ToString();
                        //var ts = t != null ? t.ToString() : "";
                        if (String.IsNullOrEmpty(ts) || !ts.Contains("System.Windows.Forms")) { continue; }

                        //Console.WriteLine(" * " + n.Expression + "." + n.Name);
                        //Console.WriteLine("\t" + "SSSEEE - " + model2.GetTypeInfo(n.Expression).Type);
                        //Console.WriteLine("\t" + "SSSNNN - " + model2.GetTypeInfo(n.Name).Type);
                        //Console.WriteLine("\t" + "SSSNN2 - " + model2.GetSymbolInfo(n.Name).Symbol.ContainingType?.ToString());

                        var ar = new AnalyzeResult();
                        ar.ProjectName = project.Name;
                        ar.SrcFilePath = document.FilePath;
                        ar.LineNumberSt = tree2.GetLineSpan(n.FullSpan).StartLinePosition.Line;
                        ar.LineNumberEd = tree2.GetLineSpan(n.FullSpan).EndLinePosition.Line;
                        ar.TypeString = model2.GetTypeInfo(n.Expression).Type.ToString();
                        ar.Name = n.Name.ToString();
                        ar.RawString = n.ToString();
                        aList.Add(ar);
                    }

                    #region gomi
                    //var methodSyntax = tree2.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();
                    //foreach (var node in tree2.GetRoot().DescendantNodes())
                    //{
                    //    Console.WriteLine(node.ToString());
                    //}
                    //var methodSymbol = model2.GetDeclaredSymbol(methodSyntax);

                    //Console.WriteLine(methodSymbol.ToString());
                    //Console.WriteLine(methodSymbol.ContainingSymbol);
                    //Console.WriteLine(methodSymbol.IsAbstract);

                    //var classDeclaration = tree2.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();
                    //foreach (var n in classDeclaration)
                    //{
                    //    Console.WriteLine("CD - " + n.Identifier.Text);
                    //}

                    //var methodDeclaration = tree2.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>();
                    //foreach (var n in methodDeclaration)
                    //{
                    //    Console.WriteLine("MD - " + n.Identifier.ToString());
                    //    Console.WriteLine("MD - " + n.Identifier.ToString());
                    //}

                    //var methodBindDeclaration = tree2.GetRoot().DescendantNodes().OfType<AnonymousMethodExpressionSyntax>();
                    //foreach (var n in methodBindDeclaration)
                    //{
                    //    Console.WriteLine("MB - " + n.ToString());
                    //}

                    //var fields = tree2.GetRoot().DescendantNodes().OfType<FieldDeclarationSyntax>();
                    //foreach (var n in fields)
                    //{
                    //    Console.WriteLine("Fie - " + n.Declaration.Type.ToString());
                    //}
                    #endregion
                }
            }

            csvAR.WriteList<AnalyzeResult>(aList);
            csvAR.CloseWriter();
        }
    }

    public class AnalyzeResult
    {
        #region プロパティ
        public string ProjectName { get; set; }
        public string SrcFilePath { get; set; }
        public int LineNumberSt { get; set; }
        public int LineNumberEd { get; set; }
        public string TypeString { get; set; }
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
        public TextReader TextFileReader { get; set; }
        public TextWriter TextFileWriter { get; set; }
        public CsvReader CsvAnalyzerResultReader { get; set; }
        public CsvWriter CsvAnalyzerResultWriter { get; set; }
        #endregion

        #region フィールド

        private String csvFilePath = "";

        #endregion

        #region コンストラクタ
        public CsvAnalyzerResult() { ; }
        #endregion

        #region メソッド
        public void SetFilePath(string path) { this.csvFilePath = path; }
        public void InitializeCsvWriter() { TextFileWriter = new StreamWriter(csvFilePath); CsvAnalyzerResultWriter = new CsvWriter(TextFileWriter); }
        public void CloseWriter()
        {
            CsvAnalyzerResultWriter.Flush();
            CsvAnalyzerResultWriter.Dispose();
            CsvAnalyzerResultWriter = null;
            TextFileWriter.Close();
            TextFileWriter = null;
        }
        public void WriteList<T>(List<T> list) { CsvAnalyzerResultWriter.WriteRecords(list); }
        public void InitializeHeader<T>() { CsvAnalyzerResultWriter.WriteHeader<T>(); CsvAnalyzerResultWriter.NextRecord(); }
        public void Flush() { CsvAnalyzerResultWriter.Flush(); }
        public IEnumerable<T> ReadAll<T>()
        {
            try
            {
                if (!File.Exists(csvFilePath)) return null;
                TextFileReader = new StreamReader(csvFilePath);
                CsvAnalyzerResultReader = new CsvReader(TextFileReader);
                return CsvAnalyzerResultReader.GetRecords<T>();
            }
            finally
            {
            }
        }
        public void CloseReader() { CsvAnalyzerResultReader.Dispose(); CsvAnalyzerResultReader = null; TextFileReader.Close(); TextFileReader = null; }
        #endregion
    }
}
