using Xunit;
using Xunit.Abstractions;
using Riken.Metabolomics.MsfinderConsoleApp.Process;
using Rfx.Riken.OsakaUniv;
using Riken.Metabolomics.MsfinderCommon.Query;
using Riken.Metabolomics.MsfinderCommon.Utility;
using Riken.Metabolomics.MsfinderCommon.Process;
using Riken.Metabolomics.StructureFinder;
using Riken.Metabolomics.StructureFinder.Parser;
using Riken.Metabolomics.StructureFinder.Utility;
using Riken.Metabolomics.StructureFinder.Property;
using Riken.Metabolomics.StructureFinder.Descriptor;
using System.IO;
using System;
using System.Linq;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using org.openscience.cdk.interfaces;
using org.openscience.cdk.silent;
using org.openscience.cdk.smiles;

namespace MsFinderTest
{
    public class MsfinderConsoleApp : IDisposable
    {
        private readonly string _projectDir;
        private readonly string _inputPath;
        private readonly string _outputPath;
        private readonly AnalysisParamOfMsfinder _param;
        private readonly List<RawData> _rawDataList;
        private readonly List<NeutralLoss> _neutralLossDB;
        private readonly List<ProductIon> _productIonDB;
        private readonly List<ExistFormulaQuery> _existFormulaDB;
        private readonly List<FragmentOntology> _fragmentOntologyDB;
        private readonly List<ChemicalOntology> _chemicalOntologies;

        public MsfinderConsoleApp(ITestOutputHelper output)
        {
            _projectDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"../../../.."));
            _inputPath = Path.Combine(_projectDir, "testdata", "input", "test.msp");
            _outputPath = Path.Combine(_projectDir, "testdata", "input", "out.msp");
            _param = MsFinderIniParcer.Read(Path.Combine(_projectDir, "MSFINDER.INI"));
            _rawDataList = RawDataParcer.RawDataFileReader(_inputPath, _param);
            _neutralLossDB = FileStorageUtility.GetNeutralLossDB();
            _productIonDB = FileStorageUtility.GetProductIonDB();
            _existFormulaDB = FileStorageUtility.GetExistFormulaDB();
            _fragmentOntologyDB = FileStorageUtility.GetUniqueFragmentDB();
            _chemicalOntologies = FileStorageUtility.GetChemicalOntologyDB();

            if (_fragmentOntologyDB != null && _productIonDB != null)
                ChemOntologyDbParser.ConvertInChIKeyToChemicalOntology(_productIonDB, _fragmentOntologyDB);
            if (_fragmentOntologyDB != null && _neutralLossDB != null)
                ChemOntologyDbParser.ConvertInChIKeyToChemicalOntology(_neutralLossDB, _fragmentOntologyDB);
            if (_fragmentOntologyDB != null && _chemicalOntologies != null)
                ChemOntologyDbParser.ConvertInChIKeyToChemicalOntology(_chemicalOntologies, _fragmentOntologyDB);
        }

        public void Dispose()
        {
            workSpaceCleanup("test.fgt", "test.sfd", "out.msp", "log_smiles.smi", "test");
            workSpaceCleanup("test_log.fgt", "test_log.sfd", "out.msp", "log_smiles.smi", "test_log");
        }

        [Fact]
        public void PeakAnnotation()
        {
            new AnnotateProcess().Run(_inputPath, Path.Combine(_projectDir, "MSFINDER.INI"), _outputPath);

            var expected = Path.Combine(_projectDir, "testdata", "expected", "out_expected.msp");

            var out_hash = GetHashCode(_outputPath);
            var expected_hash = GetHashCode(expected);

            Assert.Equal(out_hash, expected_hash);
        }

        [Fact]
        public void formulaResult()
        {
            var output = Path.Combine(_projectDir, "testdata", "input", "test.fgt");
            var expected = Path.Combine(_projectDir, "testdata", "expected", "test_expected.fgt");

            foreach (var rawData in _rawDataList)
            {
                var formulaResult = MolecularFormulaFinder.GetMolecularFormulaScore(_productIonDB, _neutralLossDB, _existFormulaDB, rawData, _param);
                var formualResults = new List<FormulaResult>() { formulaResult };
                FormulaResultParcer.FormulaResultsWriter(output, formualResults);
            }

            var out_hash = GetHashCode(output);
            var expected_hash = GetHashCode(expected);

            Assert.Equal(out_hash, expected_hash);

        }

        [Fact]
        public void FragmenterResult()
        {
            ObservableCollection<MsfinderQueryFile> queryFiles = FileStorageUtility.GetSingleAnalysisFileBeanCollection(_inputPath, _outputPath);
            foreach (var file in queryFiles)
            {
                foreach (var rawData in _rawDataList)
                {
                    PeakAssigner.Process(file, rawData, _param, _productIonDB, _neutralLossDB, _existFormulaDB, null, _fragmentOntologyDB);
                }
            }

            var output = Path.Combine(_projectDir, "testdata", "input", "test", "test.sfd");
            var expected = Path.Combine(_projectDir, "testdata", "expected", "test_expected.sfd");

            var out_hash = GetHashCode(output);
            var expected_hash = GetHashCode(expected);

            Assert.Equal(out_hash, expected_hash);
        }

        [Fact]
        public void AtomContainerToSmile()
        {
            string input_smiles = "Nc1ccc(cc1)C2(CCC(=O)NC2=O)C3CCCCC3";
            string expected = "O=C2NC(=O)C(C=1C=CC(N)=CC=1)(CC2)C3CCCCC3";
            IAtomContainer container = null;

            var smilesParser = new SmilesParser(SilentChemObjectBuilder.getInstance());
            container = smilesParser.parseSmiles(input_smiles);

            if (container != null && input_smiles.Contains('c'))
            {
                Kekulization.Kekulize(container);
            }

            var output_smiles = MoleculeConverter.AtomContainerToSmiles(container);

            Assert.Equal(output_smiles, expected);
        }
        
        [Fact]
        public void LogSmile()
        {
            new AnnotateProcess().Run(Path.Combine(_projectDir, "testdata", "input", "test_log.msp"), Path.Combine(_projectDir, "MSFINDER.INI"), _outputPath);
            var folderPath = Path.GetDirectoryName(_outputPath);
            var logSmile = Path.Combine(folderPath, "log_smiles.smi");
            var expected = Path.Combine(_projectDir, "testdata", "expected", "log_smiles_expected.smi");

            var out_hash = GetHashCode(logSmile);
            var expected_hash = GetHashCode(expected);

            Assert.Equal(out_hash, expected_hash);
        }

        public void workSpaceCleanup(string formulaFilename, string structureFilename, string outputFilename, string logFile, string folderName)
        {
            var path = Path.Combine(_projectDir, "testdata", "input");

            var formulaFile = System.IO.Directory.GetFiles(path, formulaFilename);
            if (formulaFile.Length > 0)
            {
                FileStorageUtility.DeleteFiles(formulaFile);
            }

            bool directoryExists = Directory.Exists(Path.Combine(path, folderName));

            if(directoryExists)
            {
                var structureFile = System.IO.Directory.GetFiles(Path.Combine(path, folderName), structureFilename);
                if (structureFile.Length > 0)
                {
                    FileStorageUtility.DeleteFiles(structureFile);
                }
            }

            var outputFile = System.IO.Directory.GetFiles(path, outputFilename);
            if (outputFile.Length > 0)
            {
                FileStorageUtility.DeleteFiles(outputFile);
            }

            var logSmileFile = System.IO.Directory.GetFiles(path, logFile);
            if (logSmileFile.Length > 0)
            {
                FileStorageUtility.DeleteFiles(logSmileFile);
            }
        }

        string GetHashCode(string filePath)
        {
            SHA256 sha256 = SHA256.Create();
            {
                using (var fileStream = new FileStream(filePath,
                                                       FileMode.Open,
                                                       FileAccess.Read,
                                                       FileShare.ReadWrite))
                {
                    var hash = sha256.ComputeHash(fileStream);
                    var hashString = Convert.ToBase64String(hash);
                    return hashString.TrimEnd('=');
                }
            }
        }
    }
}