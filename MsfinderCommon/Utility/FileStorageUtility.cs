using Rfx.Riken.OsakaUniv;
using Rfx.Riken.OsakaUniv.MessagePack;
using Riken.Metabolomics.MsfinderCommon.Query;
using Riken.Metabolomics.StructureFinder.Parser;
using Riken.Metabolomics.StructureFinder.Result;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
//using System.Windows;
//using System.Windows.Media;

namespace Riken.Metabolomics.MsfinderCommon.Utility {
    public sealed class FileStorageUtility {
        public static string GetResourcesPath(string file) {
            var sb = new StringBuilder();
            //var currentDir = Directory.GetCurrentDirectory();
            string workingDirectory = AppDomain.CurrentDomain.BaseDirectory;
            //sb.Append(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath));
            sb.Append(workingDirectory);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)){
                sb.Append("\\Resources").Append("\\");   
            }
            else{
                sb.Append("Resources").Append("/");
            }
            sb.Append(Properties.Resources.ResourceManager.GetString(file));
            return sb.ToString();
        }

        public static ObservableCollection<MsfinderQueryFile> GetAnalysisFileBeanCollection(string importFolderPath) {
            ObservableCollection<MsfinderQueryFile> analysisFileBeanCollection = new ObservableCollection<MsfinderQueryFile>();

            if (!Directory.Exists(importFolderPath)) return null;

            // get raw files (msp or map)
            foreach (var filePath in System.IO.Directory.GetFiles(importFolderPath, "*." + SaveFileFormat.mat, System.IO.SearchOption.TopDirectoryOnly)) {
                var analysisFileBean = new MsfinderQueryFile() { RawDataFilePath = filePath, RawDataFileName = System.IO.Path.GetFileNameWithoutExtension(filePath) };
                analysisFileBeanCollection.Add(analysisFileBean);
            }
            foreach (var filePath in System.IO.Directory.GetFiles(importFolderPath, "*." + SaveFileFormat.msp, System.IO.SearchOption.TopDirectoryOnly)) {
                var analysisFileBean = new MsfinderQueryFile() { RawDataFilePath = filePath, RawDataFileName = System.IO.Path.GetFileNameWithoutExtension(filePath) };
                analysisFileBeanCollection.Add(analysisFileBean);
            }

            // getting ismarked property
            //var param = new AnalysisParamOfMsfinder();
            //foreach (var file in analysisFileBeanCollection) {
            //    var rawdata = RawDataParcer.RawDataFileRapidReader(file.RawDataFilePath);
            //    file.BgColor = rawdata.IsMarked ? Brushes.Gray : Brushes.White;
            //}

            // set formula files and structure folder paths
            foreach (var file in analysisFileBeanCollection) {
                var formulaFilePath = importFolderPath + "/" + file.RawDataFileName + "." + SaveFileFormat.fgt;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)){
                    formulaFilePath = importFolderPath + "\\" + file.RawDataFileName + "." + SaveFileFormat.fgt;
                }
                file.FormulaFilePath = formulaFilePath;
                file.FormulaFileName = file.RawDataFileName;

                file.StructureFolderPath = importFolderPath + "/" + file.RawDataFileName;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)){
                    file.StructureFolderPath = importFolderPath + "\\" + file.RawDataFileName;
                }
                file.StructureFolderName = file.RawDataFileName;

                if (!System.IO.Directory.Exists(file.StructureFolderPath)) {
                    var di = System.IO.Directory.CreateDirectory(file.StructureFolderPath);
                }
            }
            return analysisFileBeanCollection;
        }

        public static List<Formula> GetKyusyuUnivFormulaDB(AnalysisParamOfMsfinder analysisParam) {
            var kyusyuUnivDB = FormulaKyusyuUnivDbParcer.GetFormulaBeanList(GetResourcesPath("QuickFormulaLib"), analysisParam, double.MaxValue);

            if (kyusyuUnivDB == null || kyusyuUnivDB.Count == 0) {
                return null;
            }
            else {
                return kyusyuUnivDB;
            }
        }

        public static List<ExistFormulaQuery> GetExistFormulaDB() {

            var path = GetResourcesPath("ExistFormulaLib");
            var existFormulaDB = new List<ExistFormulaQuery>();
            Console.WriteLine(path);
            try {
                existFormulaDB = MessagePackMsFinderHandler.LoadFromFile<List<ExistFormulaQuery>>(path);
            }
            catch (Exception) {
                Console.WriteLine("Error in GetExistFormulaDB to read messagepack file");
            }
            if (existFormulaDB == null || existFormulaDB.Count == 0) {
                var error = string.Empty;
                existFormulaDB = ExistFormulaDbParcer.ReadExistFormulaDB(path, out error);
                if (error != string.Empty) {
                    Console.WriteLine(error);
                }
                if (existFormulaDB == null || existFormulaDB.Count == 0) {
                    return null;
                }
                MessagePackMsFinderHandler.SaveToFile<List<ExistFormulaQuery>>(existFormulaDB, path);
            }
            return existFormulaDB;
        }

        public static List<ExistStructureQuery> GetExistStructureDB() {
            var existStructureDB = new List<ExistStructureQuery>();
            var path = GetResourcesPath("ExistStructureLib");
            try {
                existStructureDB = MessagePackMsFinderHandler.LoadFromFile<List<ExistStructureQuery>>(path);
            }
            catch (Exception) {
                Console.WriteLine("Error in GetExistStructureDB to read messagepack file");
            }
            if (existStructureDB == null || existStructureDB.Count == 0) {
                existStructureDB = ExistStructureDbParcer.ReadExistStructureDB(path);
                if (existStructureDB == null || existStructureDB.Count == 0) {
                    return null;
                }
                MessagePackMsFinderHandler.SaveToFile<List<ExistStructureQuery>>(existStructureDB, path);
            }
            ExistStructureDbParcer.SetClassyfireOntologies(existStructureDB, GetResourcesPath("InchikeyClassyfireLib"));
            return existStructureDB;

        }

        public static List<FragmentLibrary> GetEiFragmentDB() {
            var eiFragmentDB = FragmentDbParcer.ReadEiFragmentDB(GetResourcesPath("EiFragmentLib"));

            if (eiFragmentDB == null || eiFragmentDB.Count == 0) {
                return null;
            }
            else {
                return eiFragmentDB;
            }

        }

        public static List<NeutralLoss> GetNeutralLossDB() {
            var error = string.Empty;
            var neutralLossDB = FragmentDbParser.GetNeutralLossDB(GetResourcesPath("NeutralLossLib"), out error);

            if (neutralLossDB == null) {
                return null;
            }
            else {
                return neutralLossDB;
            }
        }

        public static bool IsLibrariesImported(AnalysisParamOfMsfinder param,
            List<ExistStructureQuery> eQueries, List<ExistStructureQuery> mQueries, List<ExistStructureQuery> uQueries, out string errorMessage) {
            errorMessage = string.Empty;
            if (param.IsUsePredictedRtForStructureElucidation && param.IsUseRtInchikeyLibrary) {
                var filepath = param.RtInChIKeyDictionaryFilepath;
                
                if (filepath == null || filepath == string.Empty) {
                    errorMessage = "A library containing the list of retention time and InChIKey should be selected if you select the RT option for structure elucidation.";
                    //  MessageBox.Show("A library containing the list of retention time and InChIKey should be selected if you select the RT option for structure elucidation."
                    //, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                if (!System.IO.File.Exists(filepath)) {
                    errorMessage = System.IO.Path.GetFileName(filepath) + "is not found.";
                  //  MessageBox.Show(System.IO.Path.GetFileName(filepath) + "is not found."
                  //, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                FileStorageUtility.SetRetentiontimeDataFromLibrary(eQueries, filepath);
                FileStorageUtility.SetRetentiontimeDataFromLibrary(mQueries, filepath);
                FileStorageUtility.SetRetentiontimeDataFromLibrary(uQueries, filepath);
            }

            if (param.IsUsePredictedCcsForStructureElucidation) {
                var filepath = param.CcsAdductInChIKeyDictionaryFilepath;

                if (filepath == null || filepath == string.Empty) {
                    errorMessage = "A library containing the list of CCS, adduct type and InChIKey should be selected if you select the CCS option for structure elucidation.";
                    return false;
                }

                if (!System.IO.File.Exists(filepath)) {
                    errorMessage = System.IO.Path.GetFileName(filepath) + "is not found.";
                    return false;
                }

                FileStorageUtility.SetCcsDataFromLibrary(eQueries, filepath);
                FileStorageUtility.SetCcsDataFromLibrary(mQueries, filepath);
                FileStorageUtility.SetCcsDataFromLibrary(uQueries, filepath);
            }

            return true;
        }

        public static void SetRetentiontimeDataFromLibrary(List<ExistStructureQuery> queries, string input) {

            if (queries == null || queries.Count == 0) return;
            var inchikey2Rt = new Dictionary<string, float>();
            using (var sr = new StreamReader(input, Encoding.ASCII)) { // [0] InChIKey [1] RT (min)
                sr.ReadLine();
                while (sr.Peek() > -1) {
                    var line = sr.ReadLine();
                    if (line == string.Empty) continue;
                    var lineArray = line.Split('\t');
                    if (lineArray.Length < 2) continue;

                    var inchikey = lineArray[0];
                    var shortinchikey = inchikey.Split('-')[0].Trim();
                    var rtString = lineArray[1];
                    float rt = 0.0F;
                    if (float.TryParse(rtString, out rt) && shortinchikey.Length == 14) {
                        if (!inchikey2Rt.ContainsKey(shortinchikey)) {
                            inchikey2Rt[shortinchikey] = rt;
                        }
                    }
                }
            }

            foreach (var query in queries) {
                var shortInChIkey = query.ShortInchiKey;
                if (inchikey2Rt.ContainsKey(shortInChIkey)) {
                    query.Retentiontime = inchikey2Rt[shortInChIkey];
                }
            }
        }

        public static void SetCcsDataFromLibrary(List<ExistStructureQuery> queries, string input) {

            if (queries == null || queries.Count == 0) return;
            var inchikey2AdductCcsPair = new Dictionary<string, List<string>>();
            using (var sr = new StreamReader(input, Encoding.ASCII)) { // [0] InChIKey [1] Adduct [1] CCS
                sr.ReadLine();
                while (sr.Peek() > -1) {
                    var line = sr.ReadLine();
                    if (line == string.Empty) continue;
                    var lineArray = line.Split('\t');
                    if (lineArray.Length < 3) continue;

                    var inchikey = lineArray[0];
                    var shortinchikey = inchikey.Split('-')[0].Trim();
                    var adductString = lineArray[1];
                    var adductObj = AdductIonParcer.GetAdductIonBean(adductString);
                    if (!adductObj.FormatCheck) continue;
                    var ccsString = lineArray[2];
                    float ccs = 0.0F;
                    if (float.TryParse(ccsString, out ccs) && shortinchikey.Length == 14) {

                        var pairKey = String.Join("_", new string[] { adductObj.AdductIonName, ccsString });
                        if (!inchikey2AdductCcsPair.ContainsKey(shortinchikey)) {
                            inchikey2AdductCcsPair[shortinchikey] = new List<string>() { pairKey };
                        }
                        else {
                            if (!inchikey2AdductCcsPair[shortinchikey].Contains(pairKey))
                                inchikey2AdductCcsPair[shortinchikey].Add(pairKey);
                        }
                    }
                }
            }

            foreach (var query in queries) {
                var shortInChIkey = query.ShortInchiKey;
                if (inchikey2AdductCcsPair.ContainsKey(shortInChIkey)) {
                    var adductCcsPairs = inchikey2AdductCcsPair[shortInChIkey];
                    foreach (var pair in adductCcsPairs) {
                        var adduct = pair.Split('_')[0];
                        var ccs = pair.Split('_')[1];
                        if (query.AdductToCCS == null) query.AdductToCCS = new Dictionary<string, float>();
                        query.AdductToCCS[adduct] = float.Parse(ccs);
                    }
                }
            }
        }


        public static List<ChemicalOntology> GetChemicalOntologyDB() {
            var errorMessage = string.Empty;
            var chemicalOntologies = ChemOntologyDbParser.Read(GetResourcesPath("ChemOntologyLib"), out errorMessage);

            if (chemicalOntologies == null) {
                return null;
            }
            else {
                return chemicalOntologies;
            }
        }


        public static List<FragmentOntology> GetUniqueFragmentDB() {
            var error = string.Empty;
            var uniqueFragmentDB = FragmentDbParser.GetFragmentOntologyDB(GetResourcesPath("UniqueFragmentLib"), out error);

            if (uniqueFragmentDB == null) {
                return null;
            }
            else {
                return uniqueFragmentDB;
            }
        }

        public static List<ProductIon> GetProductIonDB() {
            var error = string.Empty;
            var productIonDB = FragmentDbParser.GetProductIonDB(GetResourcesPath("ProductIonLib"), out error);

            if (productIonDB == null) {
                return null;
            }
            else {
                return productIonDB;
            }
        }

        public static List<ExistStructureQuery> GetMinesStructureDB(Formula formula) {
            var minesDB = ExistStructureDbParcer.ReadExistStructureDB(GetResourcesPath("MinesStructureLib"), formula);

            if (minesDB == null) {
                return null;
            }
            else
                return minesDB;
        }

        public static List<ExistStructureQuery> GetMinesStructureDB() {

            var path = GetResourcesPath("MinesStructureLib");
            var minesDB = new List<ExistStructureQuery>();
            try
            {
                minesDB = MessagePackMsFinderHandler.LoadFromFile<List<ExistStructureQuery>>(path);
            }
            catch (Exception)
            {
                Console.WriteLine("Error in GetMinesStructureDB to read messagepack file");
            }

            if (minesDB == null || minesDB.Count == 0) {
                minesDB = ExistStructureDbParcer.ReadExistStructureDB(path);
                if (minesDB == null || minesDB.Count == 0)
                {
                    return null;
                }
                MessagePackMsFinderHandler.SaveToFile<List<ExistStructureQuery>>(minesDB, path);
            }
            ExistStructureDbParcer.SetClassyfireOntologies(minesDB, GetResourcesPath("InchikeyClassyfireLib"));
            return minesDB;            
        }

        public static List<MspFormatCompoundInformationBean> GetInternalEiMsMsp() {
            var mspDB = MspFileParcer.MspFileReader(GetResourcesPath("EimsSpectralLib"));

            if (mspDB == null) {
                return null;
            }
            else
                return mspDB;
        }

        public static List<LbmQuery> GetLbmQueries() {
            var queries = LbmQueryParcer.GetLbmQueries(GetResourcesPath("LipidQueryMaster"), false);
            if (queries == null) {
                return null;
            }
            else
                return queries;
        }

        public static List<MspFormatCompoundInformationBean> GetInternalMsmsMsp() {
            var mspDB = MspFileParcer.MspFileReader(GetResourcesPath("MsmsSpectralLib"));

            if (mspDB == null) {
                return null;
            }
            else
                return mspDB;
        }

        public static List<MspFormatCompoundInformationBean> GetInsilicoLipidMsp() {
            //var mspDB = MspFileParcer.MspFileReader(GetResourcesPath("InsilicoLipidSpectralLib"));
            var mspDB = MspMethods.LoadMspFromFile(GetResourcesPath("InsilicoLipidSpectralLib"));

            if (mspDB == null) {
                return null;
            }
            else
                return mspDB;
        }

        public static List<MspFormatCompoundInformationBean> GetMspDB(AnalysisParamOfMsfinder param, out string errorMessage) {
            var mspDB = new List<MspFormatCompoundInformationBean>();
            errorMessage = string.Empty;
            if (param.IsUseUserDefinedSpectralDb) {
                var userDefinedDbFilePath = param.UserDefinedSpectralDbFilePath;
                if (userDefinedDbFilePath == null || userDefinedDbFilePath == string.Empty) {
                    errorMessage = "Select your own MSP database, or uncheck the user-defined spectral DB option.";
                    //MessageBox.Show("Select your own MSP database, or uncheck the user-defined spectral DB option.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
                if (!File.Exists(userDefinedDbFilePath)) {
                    errorMessage = userDefinedDbFilePath + " file is not existed.";
                    //MessageBox.Show(userDefinedDbFilePath + " file is not existed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                var userDefinedMspDB = MspFileParcer.MspFileReader(userDefinedDbFilePath);
                foreach (var mspRecord in userDefinedMspDB) mspDB.Add(mspRecord);
            }

            if (param.IsUseInternalExperimentalSpectralDb) {
                var internalMsp = new List<MspFormatCompoundInformationBean>();
                if (param.IsTmsMeoxDerivative)
                    internalMsp = GetInternalEiMsMsp();
                else
                    internalMsp = GetInternalMsmsMsp();
                foreach (var mspRecord in internalMsp) mspDB.Add(mspRecord);
            }

            if (param.IsUseInSilicoSpectralDbForLipids && param.IsTmsMeoxDerivative == false) {
                var insilicoLipidMsp = LbmFileParcer.GetSelectedLipidMspQueries(GetInsilicoLipidMsp(), param.LipidQueryBean.LbmQueries);
                foreach (var mspRecord in insilicoLipidMsp) {
                    mspDB.Add(mspRecord);
                }
            }

            if (mspDB.Count == 0) {
                errorMessage = "No spectral record.";
                //MessageBox.Show("No spectral record.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            if (param.IsPrecursorOrientedSearch)
                mspDB = mspDB.OrderBy(n => n.PrecursorMz).ToList();
            else if (param.IsTmsMeoxDerivative && !param.IsPrecursorOrientedSearch)
                mspDB = mspDB.OrderBy(n => n.RetentionIndex).ToList();

            return mspDB;
        }

        public static string GetStructureDataFilePath(string folderPath, string formula) {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)){
                return folderPath + "\\" + formula + "." + SaveFileFormat.sfd;
            }else{
                return folderPath + "/" + formula + "." + SaveFileFormat.sfd;
            }
        }

        public static void DeleteSfdFiles(string[] structureFiles) {
            foreach (var file in structureFiles) {
                File.Delete(file);
            }
        }
        public static void PeakAnnotationResultExportAsMsp(string input, AnalysisParamOfMsfinder param, string exportFilePath)
        {
            using (var sw = new StreamWriter(exportFilePath, false, Encoding.ASCII))
            {
                var files = FileStorageUtility.GetAnalysisFileBeanCollection(input);
                //var files = queryFiles;
                //var param = mainWindowVM.DataStorageBean.AnalysisParameter;
                var error = string.Empty;

                foreach (var rawfile in files)
                {
                    var rawData = RawDataParcer.RawDataFileReader(rawfile.RawDataFilePath, param);
                    var formulaResults = FormulaResultParcer.FormulaResultReader(rawfile.FormulaFilePath, out error).OrderByDescending(n => n.TotalScore).ToList();
                    if (error != string.Empty) {
                        Console.WriteLine(error);
                    }

                    var sfdFiles = System.IO.Directory.GetFiles(rawfile.StructureFolderPath);
                    var sfdResults = new List<FragmenterResult>();

                    foreach (var sfdFile in sfdFiles)
                    {
                        var sfdResult = FragmenterResultParcer.FragmenterResultReader(sfdFile);
                        var formulaString = System.IO.Path.GetFileNameWithoutExtension(sfdFile);
                        sfdResultMerge(sfdResults, sfdResult, formulaString);
                    }
                    sfdResults = sfdResults.OrderByDescending(n => n.TotalScore).ToList();
                    writeResultAsMsp(sw, rawData, formulaResults, sfdResults, param);
                }
            }
        }

        private static void writeResultAsMsp(StreamWriter sw, List<Rfx.Riken.OsakaUniv.RawData> rawDataList, List<FormulaResult> formulaResults, List<FragmenterResult> sfdResults, AnalysisParamOfMsfinder param) {
            foreach (var rawData in rawDataList) {
                sw.WriteLine("NAME: " + rawData.Name);
                sw.WriteLine("SCANNUMBER: " + rawData.ScanNumber);
                sw.WriteLine("RETENTIONTIME: " + rawData.RetentionTime);
                sw.WriteLine("RETENTIONINDEX: " + rawData.RetentionIndex);
                sw.WriteLine("PRECURSORMZ: " + rawData.PrecursorMz);
                sw.WriteLine("PRECURSORTYPE: " + rawData.PrecursorType);
                sw.WriteLine("IONMODE: " + rawData.IonMode);
                sw.WriteLine("SPECTRUMTYPE: " + rawData.SpectrumType);
                sw.WriteLine("FORMULA: " + rawData.Formula);
                sw.WriteLine("INCHIKEY: " + rawData.InchiKey);
                sw.WriteLine("INCHI: " + rawData.Inchi);
                sw.WriteLine("SMILES: " + rawData.Smiles);
                sw.WriteLine("AUTHORS: " + rawData.Authors);
                sw.WriteLine("COLLISIONENERGY: " + rawData.CollisionEnergy);
                sw.WriteLine("INSTRUMENT: " + rawData.Instrument);
                sw.WriteLine("INSTRUMENTTYPE: " + rawData.InstrumentType);
                sw.WriteLine("IONIZATION: " + rawData.Ionization);
                sw.WriteLine("LICENSE: " + rawData.License);
                sw.WriteLine("COMMENT: " + rawData.Comment);

                var spectra = rawData.Ms2Spectrum.PeakList.OrderBy(n => n.Mz).ToList();
                var maxIntensity = spectra.Max(n => n.Intensity);
                var spectraList = new List<string>();
                var ms2Peaklist = FragmentAssigner.GetCentroidMsMsSpectrum(rawData);
                //var commentList = FragmentAssigner.IsotopicPeakAssignmentForComment(ms2Peaklist, param.Mass2Tolerance, param.MassTolType);
                for (int i = 0; i < rawData.Ms2PeakNumber; i++)
                {
                    var mz = spectra[i].Mz;
                    var intensity = spectra[i].Intensity;
                    if (intensity / maxIntensity * 100 < param.RelativeAbundanceCutOff) continue;
                    var comment = "";

                    var originalComment = spectra[i].Comment;
                    var additionalComment = getProductIonComment(mz, formulaResults, sfdResults, rawData.IonMode);
                    if (originalComment != "")
                        comment = originalComment + "; " + additionalComment;
                    else
                        comment = additionalComment;

                    var peakString = string.Empty;
                    if (comment == string.Empty)
                        peakString = Math.Round(mz, 5) + "\t" + intensity;
                    else
                        peakString = Math.Round(mz, 5) + "\t" + intensity + "\t" + "\"" + comment + "\"";

                    spectraList.Add(peakString);
                }
                sw.WriteLine("Num Peaks: " + spectraList.Count);
                for (int i = 0; i < spectraList.Count; i++)
                    sw.WriteLine(spectraList[i]);

                sw.WriteLine();
            }
        }

        private static string getProductIonComment(double mz, List<FormulaResult> formulaResults, List<FragmenterResult> sfdResults, IonMode ionMode) {
            if (sfdResults == null || sfdResults.Count == 0) return string.Empty;
            if (formulaResults == null || formulaResults.Count == 0) return string.Empty;

            var productIonResult = formulaResults[0].ProductIonResult;
            var annotationResult = formulaResults[0].AnnotatedIonResult;
            var fragments = sfdResults[0].FragmentPics;
            if (fragments == null || fragments.Count == 0) { return ""; }

            foreach (var frag in fragments) {
                if (Math.Abs(frag.Peak.Mz - mz) < 0.00001) {
                    var annotation = GetLabelForInsilicoSpectrum(frag.MatchedFragmentInfo.Formula, frag.MatchedFragmentInfo.RearrangedHydrogen, ionMode, frag.MatchedFragmentInfo.AssignedAdductString);
                    var comment = "Theoretical m/z " + Math.Round(frag.MatchedFragmentInfo.MatchedMass, 6) + ", Mass diff " + Math.Round(frag.MatchedFragmentInfo.Massdiff, 3) + " (" + Math.Round(frag.MatchedFragmentInfo.Ppm, 3) + " ppm), SMILES " + frag.MatchedFragmentInfo.Smiles + ", " + "Annotation " + annotation + ", " + "Rule of HR " + frag.MatchedFragmentInfo.IsHrRule;
                    return comment;
                }
            }

            if (productIonResult.Count == 0) return string.Empty;

            foreach (var product in productIonResult) {
                if (Math.Abs(product.Mass - mz) < 0.00001) {
                    var ppm = Math.Round((mz - product.Mass) / product.Mass * 1000000, 3);
                    var comment = "Theoretical m/z " + Math.Round(product.Formula.Mass, 6) + ", Mass diff " + Math.Round(product.MassDiff, 3) +" (" + ppm + " ppm), Formula " + product.Formula.FormulaString;
                    return comment;
                }
            }

            if (annotationResult.Count == 0) return string.Empty;

            foreach (var ion in annotationResult) {
                if(Math.Abs(ion.AccurateMass - mz) < 0.00001) {                    
                    var comment = "";
                    if(ion.PeakType == AnnotatedIon.AnnotationType.Adduct) {
                        comment = "Adduct ion, " + ion.AdductIon.AdductIonName + ", linkedMz " + ion.LinkedAccurateMass;
                    }else if(ion.PeakType == AnnotatedIon.AnnotationType.Isotope) {
                        comment = "Isotopic ion, M+" + ion.IsotopeWeightNumber  + ", linkedMz " + ion.LinkedAccurateMass;
                     //   comment = "Isotopic ion, " + ion.IsotopeName + ", linkedMz " + ion.LinkedAccurateMass;
                    }
                    return comment;
                }
            }

            return string.Empty;
        }
        private static void sfdResultMerge(List<FragmenterResult> mergedList, List<FragmenterResult> results, string formulaString = "")
        {
            if (results == null || results.Count == 0) return;

            foreach (var result in results)
            {
                result.Formula = formulaString;
                mergedList.Add(result);
            }
        }
        public static string GetLabelForInsilicoSpectrum(string formula, double penalty, IonMode ionMode, string adductString)
        {
            var hydrogen = (int)Math.Abs(Math.Round(penalty, 0));
            var hydrogenString = hydrogen.ToString(); if (hydrogen == 1) hydrogenString = string.Empty;
            var ionString = string.Empty; if (ionMode == IonMode.Positive) ionString = "+"; else ionString = "-";
            var frgString = "[" + formula;

            if (penalty < 0)
            {
                frgString += "-" + hydrogenString + "H";
                if (adductString != null && adductString != string.Empty) frgString += adductString;
            }
            else if (penalty > 0)
            {
                frgString += "+" + hydrogenString + "H";
                if (adductString != null && adductString != string.Empty) frgString += adductString;
            }
            else
            {
                if (adductString != null && adductString != string.Empty) frgString += adductString;
            }

            frgString += "]" + ionString;

            return frgString;
        }
    }
}
