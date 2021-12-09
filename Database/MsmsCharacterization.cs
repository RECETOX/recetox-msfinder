﻿using Rfx.Riken.OsakaUniv;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Riken.Metabolomics.Lipidomics.Searcher {
    public sealed class LipidMsmsCharacterization {
        private LipidMsmsCharacterization() { }

        private const double Electron = 0.00054858026;
        private const double Proton = 1.00727641974;
        private const double H2O = 18.010564684;
        private const double Sugar162 = 162.052823422;

        // spectrum should be centroided, purified by absolute- and relative abundance cut off values
        // spectrum must be ordered by intensity
        // spectrum should be ObservableColllection<double[]> 0: m/z, 1: intensity
        public static LipidMolecule JudgeIfPhosphatidylcholine(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+") {
                    // seek 184.07332 (C5H15NO4P)
                    var threshold = 10.0;
                    var diagnosticMz = 184.07332;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;
                            if (sn1Carbon < 10 || sn2Carbon < 10) continue;
                            if (sn1Double > 6 || sn2Double > 6) continue;

                            var nl_SN1 = theoreticalMz - acylCainMass(sn1Carbon, sn1Double) + MassDiffDictionary.HydrogenMass;
                            var nl_SN1_H2O = nl_SN1 - H2O;

                            var nl_SN2 = theoreticalMz - acylCainMass(sn2Carbon, sn2Double) + MassDiffDictionary.HydrogenMass;
                            var nl_NS2_H2O = nl_SN2 - H2O;

                            var query = new List<Peak> {
                                new Peak() { Mz = nl_SN1, Intensity = 0.01 },
                                new Peak() { Mz = nl_SN1_H2O, Intensity = 0.01 },
                                new Peak() { Mz = nl_SN2, Intensity = 0.01 },
                                new Peak() { Mz = nl_NS2_H2O, Intensity = 0.01 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2) { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("PC", LbmClass.PC, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    ////add MT
                    //if (candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule = getLipidAnnotaionAsLevel1("PC", LbmClass.PC, totalCarbon, totalDoubleBond, score, "");
                    //    candidates.Add(molecule);
                    //}
                    //
                    return returnAnnotationResult("PC", LbmClass.PC, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);

                }
                //addMT
                else if (adduct.AdductIonName == "[M+Na]+")
                {
                    // seek 184.07332 (C5H15NO4P)
                    var threshold = 5.0;
                    var diagnosticMz = 184.07332;
                    // seek [M+Na -C5H14NO4P]+
                    var diagnosticMz2 = theoreticalMz - 183.06604;
                    // seek [M+Na -C3H9N]+
                    var diagnosticMz3 = theoreticalMz - 59.0735;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold);
                    if (isClassIonFound == false || isClassIon2Found == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var nl_SN1 = diagnosticMz3 - acylCainMass(sn1Carbon, sn1Double) - H2O + MassDiffDictionary.HydrogenMass;
                            var nl_SN2 = diagnosticMz3 - acylCainMass(sn2Carbon, sn2Double) - H2O + MassDiffDictionary.HydrogenMass;

                            var query = new List<Peak> {
                                new Peak() { Mz = nl_SN1, Intensity = 0.01 },
                                new Peak() { Mz = nl_SN2, Intensity = 0.01 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2)
                            { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("PC", LbmClass.PC, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //if (candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule = getLipidAnnotaionAsLevel1("PC", LbmClass.PC, totalCarbon, totalDoubleBond, score, "");
                    //    candidates.Add(molecule);
                    //}
                    //
                    return returnAnnotationResult("PC", LbmClass.PC, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else {
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-") {
                    // seek [M-CH3]-
                    var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - 74.036779433 : theoreticalMz - 60.021129369;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;
                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz2 = theoreticalMz - 60.021129369; // in source check
                        var isClassIonFound2 = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);
                        if (isClassIonFound2) return null;
                    }

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = fattyacidProductIon(sn1Carbon, sn1Double);
                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);

                            var query = new List<Peak> {
                            new Peak() { Mz = sn1, Intensity = 1.0 },
                            new Peak() { Mz = sn2, Intensity = 1.0 }
                        };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2) { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("PC", LbmClass.PC, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    ////add MT
                    //if (candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule = getLipidAnnotaionAsLevel1("PC", LbmClass.PC, totalCarbon, totalDoubleBond, score, "");
                    //    candidates.Add(molecule);
                    //}
                    //
                    return returnAnnotationResult("PC", LbmClass.PC, "", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfPhosphatidylethanolamine(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+") {
                    // seek -141.019094261 (C2H8NO4P)
                    var threshold = 10.0;
                    var diagnosticMz = theoreticalMz - 141.019094261;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = acylCainMass(sn1Carbon, sn1Double) - Electron;
                            var sn2 = acylCainMass(sn2Carbon, sn2Double) - Electron;

                            var query = new List<Peak> {
                            new Peak() { Mz = sn1, Intensity = 0.1 },
                            new Peak() { Mz = sn2, Intensity = 0.1 }
                        };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2) { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("PE", LbmClass.PE, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    ////add MT
                    //if (candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule = getLipidAnnotaionAsLevel1("PE", LbmClass.PE, totalCarbon, totalDoubleBond, score, "");
                    //    candidates.Add(molecule);
                    //}
                    ////
                    return returnAnnotationResult("PE", LbmClass.PE, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
                //addMT
                else if (adduct.AdductIonName == "[M+Na]+") {
                    // seek -141.019094261 (C2H8NO4P)
                    var threshold = 10.0;
                    var diagnosticMz = theoreticalMz - 141.019094261;
                    // seek - 43.042199 (C2H5N)
                    var diagnosticMz2 = theoreticalMz - 43.042199;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = acylCainMass(sn1Carbon, sn1Double) - Electron;
                            var sn2 = acylCainMass(sn2Carbon, sn2Double) - Electron;

                            var query = new List<Peak> {
                                new Peak() { Mz = sn1, Intensity = 0.01 },
                                new Peak() { Mz = sn2, Intensity = 0.01 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("PE", LbmClass.PE, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //if (candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule = getLipidAnnotaionAsLevel1("PE", LbmClass.PE, totalCarbon, totalDoubleBond, score, "");
                    //    candidates.Add(molecule);
                    //}

                    return returnAnnotationResult("PE", LbmClass.PE, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else {
                if (adduct.AdductIonName == "[M-H]-") {
                    // seek C5H11NO5P-
                    var threshold = 5.0;
                    var diagnosticMz = 196.03803;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    var threshold2 = 5.0;
                    var diagnosticMz2 = 152.995833871; // seek C3H6O5P- (maybe LNAPE)
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon2Found == true) return null;


                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = fattyacidProductIon(sn1Carbon, sn1Double);
                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);

                            var query = new List<Peak> {
                            new Peak() { Mz = sn1, Intensity = 1.0 },
                            new Peak() { Mz = sn2, Intensity = 1.0 }
                        };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2) { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("PE", LbmClass.PE, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }

                    if (isClassIonFound == false && candidates.Count == 0) return null;

                    return returnAnnotationResult("PE", LbmClass.PE, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }


        public static LipidMolecule JudgeIfPhosphatidylserine(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PS 46:6, totalCarbon = 46 and totalDoubleBond = 6
           int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
           AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {
                    // seek -185.008927 (C3H8NO6P)
                    var threshold = 10.0;
                    var diagnosticMz = theoreticalMz - 185.008927;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var nl_SN1 = theoreticalMz - acylCainMass(sn1Carbon, sn1Double) + MassDiffDictionary.HydrogenMass;
                            var nl_SN1_H2O = nl_SN1 - H2O;

                            var nl_SN2 = theoreticalMz - acylCainMass(sn2Carbon, sn2Double) + MassDiffDictionary.HydrogenMass;
                            var nl_NS2_H2O = nl_SN2 - H2O;

                            var query = new List<Peak> {
                                new Peak() { Mz = nl_SN1, Intensity = 0.01 },
                                new Peak() { Mz = nl_SN1_H2O, Intensity = 0.01 },
                                new Peak() { Mz = nl_SN2, Intensity = 0.01 },
                                new Peak() { Mz = nl_NS2_H2O, Intensity = 0.01 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2)
                            { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("PS", LbmClass.PS, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    ////add MT
                    //if (candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule = getLipidAnnotaionAsLevel1("PS", LbmClass.PS, totalCarbon, totalDoubleBond, score, "");
                    //    candidates.Add(molecule);
                    //}
                    //
                    return returnAnnotationResult("PS", LbmClass.PS, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 2);

                }
                else if (adduct.AdductIonName == "[M+Na]+")
                {
                    // seek -185.008927 (C3H8NO6P)
                    var threshold = 10.0;
                    var diagnosticMz = theoreticalMz - 185.008927;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // acyl level may be not able to annotate.
                    var candidates = new List<LipidMolecule>();
                    //var score = 0;
                    //var molecule = getLipidAnnotaionAsLevel1("PS", LbmClass.PS, totalCarbon, totalDoubleBond, score, "");
                    //candidates.Add(molecule);

                    return returnAnnotationResult("PS", LbmClass.PS, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else {
                if (adduct.AdductIonName == "[M-H]-") {
                    // seek C3H5NO2 loss
                    var threshold = 10.0;
                    var diagnosticMz = theoreticalMz - 87.032029;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    var threshold2 = 30;
                    var diagnosticMz2 = theoreticalMz - 63.008491; // [M+C2H3N(ACN)+Na-2H]- adduct of PG [M-H]- 
                    var isClassIonFound2 = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIonFound2) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = fattyacidProductIon(sn1Carbon, sn1Double);
                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);

                            var query = new List<Peak> {
                                new Peak() { Mz = sn1, Intensity = 1.0 },
                                new Peak() { Mz = sn2, Intensity = 1.0 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2) { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("PS", LbmClass.PS, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    ////add MT
                    //if (candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule = getLipidAnnotaionAsLevel1("PS", LbmClass.PS, totalCarbon, totalDoubleBond, score, "");
                    //    candidates.Add(molecule);
                    //}
                    ////
                    return returnAnnotationResult("PS", LbmClass.PS, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfPhosphatidylglycerol(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PS 46:6, totalCarbon = 46 and totalDoubleBond = 6
           int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
           AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+") {
                    // seek -189.040227 (C3H8O6P+NH4)
                    var threshold = 10.0;
                    var diagnosticMz = theoreticalMz - 189.040227;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {
                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;


                            var nl_SN1 = diagnosticMz - acylCainMass(sn1Carbon, sn1Double) + MassDiffDictionary.HydrogenMass;
                            var nl_SN2 = diagnosticMz - acylCainMass(sn2Carbon, sn2Double) + MassDiffDictionary.HydrogenMass;

                            var query = new List<Peak> {
                                new Peak() { Mz = nl_SN1, Intensity = 0.01 },
                                new Peak() { Mz = nl_SN2, Intensity = 0.01 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2 && averageIntensity < 30) { // average intensity < 30 is nessesarry to distinguish it from BMP
                                var molecule = getPhospholipidMoleculeObjAsLevel2("PG", LbmClass.PG, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    ////add MT
                    //if (candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule = getLipidAnnotaionAsLevel1("PG", LbmClass.PG, totalCarbon, totalDoubleBond, score, "");
                    //    candidates.Add(molecule);
                    //}
                    ////
                    return returnAnnotationResult("PG", LbmClass.PG, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
                else if (adduct.AdductIonName == "[M+Na]+")
                {
                    // seek -171.005851 (C3H8O6P) - 22.9892207 (Na+)
                    var threshold = 10.0;
                    var diagnosticMz = theoreticalMz - 171.005851 - 22.9892207;// + MassDiffDictionary.HydrogenMass;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    //var score = 0;
                    //var molecule = getLipidAnnotaionAsLevel1("PG", LbmClass.PG, totalCarbon, totalDoubleBond, score, "");
                    //candidates.Add(molecule);

                    return returnAnnotationResult("PG", LbmClass.PG, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);

                }
            }
            else {
                if (adduct.AdductIonName == "[M-H]-") {
                    // seek C3H6O5P-
                    var threshold = 0.01;
                    var diagnosticMz = 152.995833871;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = fattyacidProductIon(sn1Carbon, sn1Double);
                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);

                            var query = new List<Peak> {
                                new Peak() { Mz = sn1, Intensity = 1.0 },
                                new Peak() { Mz = sn2, Intensity = 1.0 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2) { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("PG", LbmClass.PG, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //if (isClassIonFound == false && candidates.Count == 0) return null;
                    if (candidates.Count == 0) return null;

                    return returnAnnotationResult("PG", LbmClass.PG, "", theoreticalMz, adduct,
                      totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfBismonoacylglycerophosphate(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PS 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct) {

            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+") {
                    // seek -189.040227 (C3H8O6P+NH4)
                    var threshold = 0.01;
                    var diagnosticMz = theoreticalMz - 189.040227;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    //if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {
                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var nl_SN1 = diagnosticMz - acylCainMass(sn1Carbon, sn1Double) + MassDiffDictionary.HydrogenMass;
                            var nl_SN2 = diagnosticMz - acylCainMass(sn2Carbon, sn2Double) + MassDiffDictionary.HydrogenMass;

                            var query = new List<Peak> {
                                new Peak() { Mz = nl_SN1, Intensity = 10 },
                                new Peak() { Mz = nl_SN2, Intensity = 10 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2 && averageIntensity >= 30) { // average intensity < 30 is nessesarry to distinguish it from BMP
                                var molecule = getPhospholipidMoleculeObjAsLevel2("BMP", LbmClass.BMP, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    ////add MT
                    //if (candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule = getLipidAnnotaionAsLevel1("BMP", LbmClass.BMP, totalCarbon, totalDoubleBond, score, "");
                    //    candidates.Add(molecule);
                    //}
                    //
                    if (candidates.Count == 0) return null;
                    return returnAnnotationResult("BMP", LbmClass.BMP, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);

                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfPhosphatidylinositol(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PS 46:6, totalCarbon = 46 and totalDoubleBond = 6
           int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
           AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+") {
                    // seek -277.056272 (C6H12O9P+NH4)
                    var threshold = 10.0;
                    var diagnosticMz = theoreticalMz - 277.056272;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    #region // now, acyl position is not evaluated in PI positive, but it will be dealt in future
                    //for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                    //    for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {
                    //        var sn2Carbon = totalCarbon - sn1Carbon;
                    //        var sn2Double = totalDoubleBond - sn1Double;

                    //        var nl_SN1 = diagnosticMz - acylCainMass(sn1Carbon, sn1Double);
                    //        var nl_SN2 = diagnosticMz - acylCainMass(sn2Carbon, sn2Double);

                    //        var query = new List<Peak> {
                    //            new Peak() { Mz = nl_SN1, Intensity = 0.01 },
                    //            new Peak() { Mz = nl_SN2, Intensity = 0.01 }
                    //        };

                    //        var foundCount = 0;
                    //        var averageIntensity = 0.0;
                    //        countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                    //        if (foundCount == 2) { // now I set 2 as the correct level
                    //            var molecule = getPhospholipidMoleculeObjAsLevel2("PI", LbmClass.PI, sn1Carbon, sn1Double,
                    //                sn2Carbon, sn2Double, averageIntensity);
                    //            candidates.Add(molecule);
                    //        }
                    //    }
                    //}
                    #endregion

                    return returnAnnotationResult("PI", LbmClass.PI, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
                else if (adduct.AdductIonName == "[M+Na]+")
                {
                    // 
                    var threshold = 10.0;
                    var diagnosticMz1 = theoreticalMz - (259.021895 + 22.9892207);  // seek -(C6H12O9P +Na)
                    var diagnosticMz2 = theoreticalMz - (260.02972);                 // seek -(C6H12O9P + H)
                    var diagnosticMz3 = (260.02972 + 22.9892207);                   // seek (C6H13O9P +Na)

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);
                    var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold);
                    if (!isClassIon1Found || !isClassIon2Found || !isClassIon3Found) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    //var score = 0;
                    //var molecule = getLipidAnnotaionAsLevel1("PI", LbmClass.PI, totalCarbon, totalDoubleBond, score, "");
                    //candidates.Add(molecule);

                    return returnAnnotationResult("PI", LbmClass.PI, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 1);
                }

            }
            else {
                if (adduct.AdductIonName == "[M-H]-") {
                    // seek 241.01188(C6H10O8P-) and 297.037548(C9H14O9P-)
                    var threshold = 0.01;
                    var diagnosticMz1 = 241.01188 + Electron;
                    var diagnosticMz2 = 297.037548 + Electron;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);
                    if (!isClassIon1Found && !isClassIon2Found) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = fattyacidProductIon(sn1Carbon, sn1Double);
                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);

                            var query = new List<Peak> {
                                new Peak() { Mz = sn1, Intensity = 0.01 },
                                new Peak() { Mz = sn2, Intensity = 0.01 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2) { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("PI", LbmClass.PI, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    ////add MT
                    //if (candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule = getLipidAnnotaionAsLevel1("PI", LbmClass.PI, totalCarbon, totalDoubleBond, score, "");
                    //    candidates.Add(molecule);
                    //}
                    ////

                    return returnAnnotationResult("PI", LbmClass.PI, "", theoreticalMz, adduct,
                      totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfNacylphosphatidylserine(ObservableCollection<double[]> spectrum, double ms2Tolerance,
          double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
          int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
          AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+") //// not found in lipidDbProject-Pos
                {
                    // check [M-C2H8NO4P+H]+  
                    var threshold = 1;
                    var diagnosticMz = theoreticalMz - 141.019094261;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (!isClassIon1Found) return null; // reject PS

                    // from here, acyl level annotation is executed. 
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var nl_SN1 = theoreticalMz - acylCainMass(sn1Carbon, sn1Double) + MassDiffDictionary.HydrogenMass;
                            var nl_SN1_H2O = nl_SN1 - H2O;

                            var query = new List<Peak> {
                                new Peak() { Mz = nl_SN1, Intensity = 0.01 },
                                new Peak() { Mz = nl_SN1_H2O, Intensity = 0.01 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2)
                            { // now I set 2 as the correct level
                                var molecule = getNacylphospholipidMoleculeObjAsLevel2("LNAPS", LbmClass.LNAPS, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }

                return returnAnnotationResult("LNAPS", LbmClass.LNAPS, "", theoreticalMz, adduct,
                    totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }

            else
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek C5H11NO5P- is not observed in LNAPE, instead, C3H6O5P is observed.
                    var threshold = 10;
                    var diagnosticMz1 = 152.995833871;
                    var diagnosticMz2 = theoreticalMz - 87.032029; // should be a marker of PS
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);
                    if (!isClassIon1Found || isClassIon2Found) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            //if (sn2Carbon < minSnCarbon) { break; }

                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = fattyacidProductIon(sn1Carbon, sn1Double);
                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);

                            var nl_SN2 = diagnosticMz2 - acylCainMass(sn2Carbon, sn2Double) + MassDiffDictionary.HydrogenMass;

                            //Debug.WriteLine("LNAPS {0}:{1}/n-{2}:{3}, m/z {4}", sn1Carbon, sn1Double, sn2Carbon, sn2Double, nl_SN2);

                            var query = new List<Peak> {
                                new Peak() { Mz = nl_SN2, Intensity = 30.0 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);
                            if (foundCount == 1)
                            {
                                query = new List<Peak> {
                                    new Peak() { Mz = sn1, Intensity = 30.0 },
                                    //new Peak() { Mz = nl_SN1, Intensity = 10.0 }
                                };
                                foundCount = 0;
                                averageIntensity = 0.0;
                                countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                if (foundCount == 0)
                                { // LNAPS should become 0
                                    var molecule = getNacylphospholipidMoleculeObjAsLevel2("LNAPS", LbmClass.LNAPS, sn1Carbon, sn1Double,
                                        sn2Carbon, sn2Double, averageIntensity);
                                    candidates.Add(molecule);
                                }
                            }
                        }
                    }
                    if (candidates.Count == 0) return null;
                    return returnAnnotationResult("LNAPS", LbmClass.LNAPS, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfNacylphosphatidylethanolamine(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode  //// not found in lipidDbProject-Pos
                if (adduct.AdductIonName == "[M+H]+")
                {
                    //// check -141.019094261 (C2H8NO4P)
                    //var threshold = 1.0;
                    //var diagnosticMz = theoreticalMz - 141.019094261;

                    //var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    //if (isClassIonFound == true) return null;  // reject PE

                    //// from here, acyl level annotation is executed. 
                    //var candidates = new List<LipidMolecule>();
                    //for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    //{
                    //    for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                    //    {

                    //        var sn2Carbon = totalCarbon - sn1Carbon;
                    //        var sn2Double = totalDoubleBond - sn1Double;
                    //        var sn1 = etherBondAcylLoss(sn1Carbon, sn1Double) - Electron;
                    //        var query = new List<Peak> {
                    //            new Peak() { Mz = sn1, Intensity = 0.1 },
                    //        };

                    //        var foundCount = 0;
                    //        var averageIntensity = 0.0;
                    //        countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                    //        if (foundCount == 2)
                    //        { // now I set 2 as the correct level
                    //            var molecule = getNacylphospholipidMoleculeObjAsLevel2("LNAPE", LbmClass.LNAPE, sn1Carbon, sn1Double,
                    //                sn2Carbon, sn2Double, averageIntensity);
                    //            candidates.Add(molecule);
                    //        }
                    //    }
                    //}
                    ////if (candidates.Count == 0)
                    ////{
                    ////    var score = 0;
                    ////    var molecule = getLipidAnnotaionAsLevel1("LNAPE", LbmClass.LNAPE, totalCarbon, totalDoubleBond, score, "");
                    ////    candidates.Add(molecule);
                    ////}
                    //return returnAnnotationResult("LNAPE", LbmClass.LNAPE, "", theoreticalMz, adduct,
                    //    totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek C5H11NO5P- is not observed in LNAPE, instead, C3H6O5P is observed.
                    var threshold = 0.01;
                    var diagnosticMz1 = 196.03803; // should be a marker of PE
                    var diagnosticMz2 = 152.995833871;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);
                    if (!isClassIon2Found || isClassIon1Found) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;
                            //if (sn2Carbon < minSnCarbon) { break; }

                            var sn1 = fattyacidProductIon(sn1Carbon, sn1Double);
                            var nl_SN1 = theoreticalMz - acylCainMass(sn1Carbon, sn1Double) + MassDiffDictionary.HydrogenMass;
                            var nl_SN1_H2O = nl_SN1 - H2O;

                            var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, sn1, threshold); 
                            if (!isClassIon3Found) return null; // sn1 must (2019/10/24)


                            //Console.WriteLine(sn1Carbon + ":" + sn1Double + "/n-" + sn2Carbon + ":" + sn2Double +
                            //    " " + sn1 + " " + nl_SN1);

                            var query = new List<Peak> {
                                new Peak() { Mz = sn1, Intensity = 30.0 },
                                new Peak() { Mz = nl_SN1, Intensity = 1 },
                                new Peak() { Mz = nl_SN1_H2O, Intensity = 1 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2)
                            { // now I set 2 as the correct level
                                var molecule = getNacylphospholipidMoleculeObjAsLevel2("LNAPE", LbmClass.LNAPE, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //var score = 0;
                    //var molecule0 = getLipidAnnotaionAsLevel1("LNAPE", LbmClass.LNAPE, totalCarbon, totalDoubleBond, score, "");
                    //candidates.Add(molecule0);
                    if (candidates.Count == 0) return null;
                    return returnAnnotationResult("LNAPE", LbmClass.LNAPE, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }


        public static LipidMolecule JudgeIfSphingomyelin(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
            AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {
                    // seek 184.07332 (C5H15NO4P)
                    var threshold = 30.0;
                    var diagnosticMz = 184.07332;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++)
                        {
                            if (sphCarbon <= 13) continue;
                            if (sphCarbon == 16 && sphDouble >= 3) continue;
                            var acylCarbon = totalCarbon - sphCarbon;
                            var acylDouble = totalDoubleBond - sphDouble;
                            if (acylCarbon < 8) continue;


                            var sph1 = theoreticalMz - acylCainMass(acylCarbon, acylDouble) -
                                diagnosticMz - H2O + 2 * MassDiffDictionary.HydrogenMass;

                            var query = new List<Peak> {
                                new Peak() { Mz = sph1, Intensity = 0.01 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 1)
                            { // the diagnostic acyl ion must be observed for level 2 annotation
                                var molecule = getCeramideMoleculeObjAsLevel2("SM", LbmClass.SM, "d", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("SM", LbmClass.SM, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}

                    return returnAnnotationResult("SM", LbmClass.SM, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);

                }
                else if (adduct.AdductIonName == "[M+Na]+")
                {
                    // seek -59.0735 [M-C3H9N+Na]+
                    var threshold = 20.0;
                    var diagnosticMz = theoreticalMz - 59.0735;
                    // seek -183.06604 [M-C5H14NO4P+Na]+
                    var threshold2 = 30.0;
                    var diagnosticMz2 = theoreticalMz - 183.06604;
                    // seek -183.06604 [M-C5H16NO5P+H]+
                    var threshold3 = 1;
                    var diagnosticMz3 = theoreticalMz - 183.06604 - 39.993064;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                    //if (isClassIonFound == !true || isClassIon2Found == !true || isClassIon3Found == !true) return null;
                    if (isClassIonFound == false || isClassIon2Found == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    //for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    //{
                    //    for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++)
                    //    {
                    //        var acylCarbon = totalCarbon - sphCarbon;
                    //        //if (acylCarbon < minSphCarbon) { break;  }
                    //        var acylDouble = totalDoubleBond - sphDouble;

                    //        var sph1 = diagnosticMz2 - acylCainMass(acylCarbon, acylDouble) 
                    //                -  (MassDiffDictionary.HydrogenMass*2) - 22.9898;

                    //        var query = new List<Peak> {
                    //            new Peak() { Mz = sph1, Intensity = 0.01 }
                    //        };

                    //        var foundCount = 0;
                    //        var averageIntensity = 0.0;
                    //        countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                    //        if (foundCount == 1)
                    //        { // the diagnostic acyl ion must be observed for level 2 annotation
                    //            var molecule = getCeramideMoleculeObjAsLevel2("SM", LbmClass.SM, "d", sphCarbon, sphDouble,
                    //                acylCarbon, acylDouble, averageIntensity);
                    //            candidates.Add(molecule);
                    //        }
                    //    }
                    //}
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("SM", LbmClass.SM, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}

                    return returnAnnotationResult("SM", LbmClass.SM, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);

                }

            }
                else {
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-") {
                    // seek [M-CH3]-
                    var threshold1 = 50.0;
                    var threshold2 = 0.01;
                    var diagnosticMz1 = adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - 74.036779433 : theoreticalMz - 60.021129369;
                    var diagnosticMz2 = 168.042572 + Electron;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon1Found != true || isClassIon2Found != true) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++) {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) {

                            var acylCarbon = totalCarbon - sphCarbon;
                            var acylDouble = totalDoubleBond - sphDouble;

                            if (acylCarbon < 8) continue;

                            var sphFragment = diagnosticMz1 - acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.HydrogenMass;
                            var query = new List<Peak> {
                                new Peak() { Mz = sphFragment, Intensity = 0.01 }
                            };
                            //if (sphCarbon == 18 && sphDouble == 1 && acylCarbon == 17 && acylDouble == 0) {
                            //    Console.WriteLine("");
                            //}

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 1) { // the diagnostic acyl ion must be observed for level 2 annotation
                                var molecule = getCeramideMoleculeObjAsLevel2("SM", LbmClass.SM, "d", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("SM", LbmClass.SM, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}

                    return returnAnnotationResult("SM", LbmClass.SM, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfSphingomyelinPhyto(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
            AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+") {
                    // seek 184.07332 (C5H15NO4P)
                    var threshold = 30.0;
                    var diagnosticMz = 184.07332;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    return returnAnnotationResult("SM", LbmClass.SM, "t", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);

                }

            }
            else {
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-") {
                    // seek [M-CH3]-
                    var threshold1 = 50.0;
                    var threshold2 = 0.01;
                    var diagnosticMz1 = adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - 74.036779433 : theoreticalMz - 60.021129369;
                    var diagnosticMz2 = 168.042572 + Electron;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon1Found != true || isClassIon2Found != true) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    return returnAnnotationResult("SM", LbmClass.SM, "t", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfTriacylglycerol(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PS 46:6, totalCarbon = 46 and totalDoubleBond = 6
           int minSn1Carbon, int maxSn1Carbon, int minSn1DoubleBond, int maxSn1DoubleBond,
           int minSn2Carbon, int maxSn2Carbon, int minSn2DoubleBond, int maxSn2DoubleBond,
           AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSn1Carbon > totalCarbon) maxSn1Carbon = totalCarbon;
            if (maxSn1DoubleBond > totalDoubleBond) maxSn1DoubleBond = totalDoubleBond;
            if (maxSn2Carbon > totalCarbon) maxSn2Carbon = totalCarbon;
            if (maxSn2DoubleBond > totalDoubleBond) maxSn2DoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // seek -17.026549 (NH3)
                    var threshold = 1.0;
                    var diagnosticMz = theoreticalMz - 17.026549;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSn1Carbon; sn1Carbon <= maxSn1Carbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSn1DoubleBond; sn1Double <= maxSn1DoubleBond; sn1Double++)
                        {

                            var remainCarbon = totalCarbon - sn1Carbon;
                            var remainDouble = totalDoubleBond - sn1Double;
                            var carbonLimit = Math.Min(remainCarbon, maxSn2Carbon);
                            var doubleLimit = Math.Min(remainDouble, maxSn2DoubleBond);

                            for (int sn2Carbon = minSn2Carbon; sn2Carbon <= carbonLimit; sn2Carbon++)
                            {
                                for (int sn2Double = minSn2DoubleBond; sn2Double <= doubleLimit; sn2Double++)
                                {

                                    var sn3Carbon = totalCarbon - sn1Carbon - sn2Carbon;
                                    var sn3Double = totalDoubleBond - sn1Double - sn2Double;

                                    if ((sn1Carbon == 18 && sn1Double == 5) || (sn2Carbon == 18 && sn2Double == 5) || (sn3Carbon == 18 && sn3Double == 5)) continue;

                                    var nl_SN1 = diagnosticMz - acylCainMass(sn1Carbon, sn1Double) - H2O + MassDiffDictionary.HydrogenMass;
                                    var nl_SN2 = diagnosticMz - acylCainMass(sn2Carbon, sn2Double) - H2O + MassDiffDictionary.HydrogenMass;
                                    var nl_SN3 = diagnosticMz - acylCainMass(sn3Carbon, sn3Double) - H2O + MassDiffDictionary.HydrogenMass;
                                    var query = new List<Peak> {
                                        new Peak() { Mz = nl_SN1, Intensity = 5 },
                                        new Peak() { Mz = nl_SN2, Intensity = 5 },
                                        new Peak() { Mz = nl_SN3, Intensity = 5 }
                                    };

                                    var foundCount = 0;
                                    var averageIntensity = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                    if (foundCount == 3)
                                    { // these three chains must be observed.
                                        var molecule = getTriacylglycerolMoleculeObjAsLevel2("TG", LbmClass.TG, sn1Carbon, sn1Double,
                                            sn2Carbon, sn2Double, sn3Carbon, sn3Double, averageIntensity);
                                        candidates.Add(molecule);
                                    }
                                }
                            }
                        }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getLipidAnnotaionAsLevel1("TAG", LbmClass.TAG, totalCarbon, totalDoubleBond, score, "");
                    //    candidates.Add(molecule0);
                    //}
                    if (candidates == null || candidates.Count == 0) return null;
                    return returnAnnotationResult("TG", LbmClass.TG, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 3);
                }
                else if (adduct.AdductIonName == "[M+Na]+")
                {   //add MT
                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSn1Carbon; sn1Carbon <= maxSn1Carbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSn1DoubleBond; sn1Double <= maxSn1DoubleBond; sn1Double++)
                        {
                            var diagnosticMz = theoreticalMz; // - 22.9892207 + MassDiffDictionary.HydrogenMass; //if want to choose [M+H]+
                            var remainCarbon = totalCarbon - sn1Carbon;
                            var remainDouble = totalDoubleBond - sn1Double;
                            var carbonLimit = Math.Min(remainCarbon, maxSn2Carbon);
                            var doubleLimit = Math.Min(remainDouble, maxSn2DoubleBond);

                            for (int sn2Carbon = minSn2Carbon; sn2Carbon <= carbonLimit; sn2Carbon++)
                            {
                                for (int sn2Double = minSn2DoubleBond; sn2Double <= doubleLimit; sn2Double++)
                                {

                                    var sn3Carbon = totalCarbon - sn1Carbon - sn2Carbon;
                                    var sn3Double = totalDoubleBond - sn1Double - sn2Double;

                                    var nl_SN1 = diagnosticMz - acylCainMass(sn1Carbon, sn1Double) - H2O + MassDiffDictionary.HydrogenMass;
                                    var nl_SN2 = diagnosticMz - acylCainMass(sn2Carbon, sn2Double) - H2O + MassDiffDictionary.HydrogenMass;
                                    var nl_SN3 = diagnosticMz - acylCainMass(sn3Carbon, sn3Double) - H2O + MassDiffDictionary.HydrogenMass;
                                    var query = new List<Peak> {
                                        new Peak() { Mz = nl_SN1, Intensity = 0.1 },
                                        new Peak() { Mz = nl_SN2, Intensity = 0.1 },
                                        new Peak() { Mz = nl_SN3, Intensity = 0.1 }
                                    };

                                    var foundCount = 0;
                                    var averageIntensity = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                    if (foundCount < 3)
                                    {
                                        var diagnosticMzH = theoreticalMz - 22.9892207 + MassDiffDictionary.HydrogenMass;
                                        var nl_SN1_H = diagnosticMzH - acylCainMass(sn1Carbon, sn1Double) - H2O + MassDiffDictionary.HydrogenMass;
                                        var nl_SN2_H = diagnosticMzH - acylCainMass(sn2Carbon, sn2Double) - H2O + MassDiffDictionary.HydrogenMass;
                                        var nl_SN3_H = diagnosticMzH - acylCainMass(sn3Carbon, sn3Double) - H2O + MassDiffDictionary.HydrogenMass;
                                        var query2 = new List<Peak> {
                                        new Peak() { Mz = nl_SN1_H, Intensity = 0.1 },
                                        new Peak() { Mz = nl_SN2_H, Intensity = 0.1 },
                                        new Peak() { Mz = nl_SN3_H, Intensity = 0.1 }
                                        };

                                        var foundCount2 = 0;
                                        var averageIntensity2 = 0.0;
                                        countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity2);


                                        if (foundCount2 == 3)
                                        {
                                            var molecule = getTriacylglycerolMoleculeObjAsLevel2("TG", LbmClass.TG, sn1Carbon, sn1Double,
                                            sn2Carbon, sn2Double, sn3Carbon, sn3Double, averageIntensity2);
                                            candidates.Add(molecule);
                                        }
                                    }
                                    else
                                    if (foundCount == 3)
                                    { // these three chains must be observed.
                                        var molecule = getTriacylglycerolMoleculeObjAsLevel2("TG", LbmClass.TG, sn1Carbon, sn1Double,
                                            sn2Carbon, sn2Double, sn3Carbon, sn3Double, averageIntensity);
                                        candidates.Add(molecule);
                                    }
                                }
                            }
                        }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getLipidAnnotaionAsLevel1("TAG", LbmClass.TAG, totalCarbon, totalDoubleBond, score, "");
                    //    candidates.Add(molecule0);
                    //}
                    if (candidates == null || candidates.Count == 0) return null;
                    return returnAnnotationResult("TG", LbmClass.TG, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 3);
                }
            }
            else {

            }
            return null;
        }

        public static LipidMolecule JudgeIfAcylcarnitine(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
            int totalCarbon, int totalDoubleBond, AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M]+") {
                    // seek 85.028405821 (C4H5O2+)
                    var threshold = 5.0;
                    var diagnosticMz = 85.028405821;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // seek 59.073499294 loss (C3H9N)
                    var diagnosticMz2 = theoreticalMz - 59.073499294;
                    var threshold2 = 1.0;
                    var isClassIonFound2 = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);

                    // seek 144.1019 (Acyl and H2O loss) // not found at PasefOn case
                    var diagnosticMz3 = 144.1019;
                    var threshold3 = 1.0;
                    var isClassIonFound3 = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);

                    // acyl fragment // found at PasefOn case (but little bit tight)
                    var acylFragment = acylCainMass(totalCarbon, totalDoubleBond) - Electron;
                    var threshold4 = 0.1;
                    var isClassIonFound4 = isDiagnosticFragmentExist(spectrum, ms2Tolerance, acylFragment, threshold4);

                    // acyl fragment // found at PasefOn case (but little bit tight)
                    var threshold5 = 99;
                    var isClassIonFound5 = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold5);

                    //if (!isClassIonFound) return null;
                    if (!isClassIonFound) {
                        if (isClassIonFound5 || !isClassIonFound2) return null;
                    }
                    if (isClassIonFound == false) {
                        if (isClassIonFound2 == false || isClassIonFound3 == false) {
                            if (isClassIonFound2 == false || isClassIonFound4 == false) return null;
                        }
                        if (isClassIonFound5 || !isClassIonFound2) return null;
                    };

                    var candidates = new List<LipidMolecule>();
                    //var averageIntensity = 0.0;
                    //var molecule = getSingleacylchainMoleculeObjAsLevel2("ACar", LbmClass.ACar, totalCarbon, totalDoubleBond,
                    //               averageIntensity);
                    //candidates.Add(molecule);

                    return returnAnnotationResult("CAR", LbmClass.CAR, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);

                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfCholesterylEster(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
            int totalCarbon, int totalDoubleBond, AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+") {
                    // seek 369.3515778691 (C27H45+)
                    var threshold = 60.0;
                    var diagnosticMz = 369.3515778691;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;
                    if (totalCarbon >= 41 && totalDoubleBond >= 4) return null;

                    var candidates = new List<LipidMolecule>();
                    //var averageIntensity = 0.0;
                    //var molecule = getSingleacylchainMoleculeObjAsLevel2("CE", LbmClass.CE, totalCarbon, totalDoubleBond,
                    //               averageIntensity);
                    //candidates.Add(molecule);

                    return returnAnnotationResult("CE", LbmClass.CE, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
                else if (adduct.AdductIonName == "[M+Na]+")
                {
                    // seek 369.3515778691 (C27H45+)
                    var threshold = 10.0;
                    var diagnosticMz = 369.3515778691;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    // if (isClassIonFound == false) return null;

                    var candidates = new List<LipidMolecule>();
                    //var averageIntensity = 0.0;
                    //var molecule = getSingleacylchainMoleculeObjAsLevel2("CE", LbmClass.CE, totalCarbon, totalDoubleBond,
                    //               averageIntensity);
                    //candidates.Add(molecule);

                    return returnAnnotationResult("CE", LbmClass.CE, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }

            }
            return null;
        }

        //add MT
        public static LipidMolecule JudgeIfDag(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PS 46:6, totalCarbon = 46 and totalDoubleBond = 6
           int minSn1Carbon, int maxSn1Carbon, int minSn1DoubleBond, int maxSn1DoubleBond,
           AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSn1Carbon > totalCarbon) maxSn1Carbon = totalCarbon;
            if (maxSn1DoubleBond > totalDoubleBond) maxSn1DoubleBond = totalDoubleBond;
            if (totalCarbon > 52) return null; // currently, very large DAG is excluded.
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // seek -17.026549 (NH3)
                    var threshold = 1.0;
                    var diagnosticMz = theoreticalMz - 17.026549;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSn1Carbon; sn1Carbon <= maxSn1Carbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSn1DoubleBond; sn1Double <= maxSn1DoubleBond; sn1Double++)
                        {
                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;
                            if (sn2Double >= 7) continue;

                            var nl_SN1 = diagnosticMz - acylCainMass(sn1Carbon, sn1Double) - H2O + MassDiffDictionary.HydrogenMass;
                            var nl_SN2 = diagnosticMz - acylCainMass(sn2Carbon, sn2Double) - H2O + MassDiffDictionary.HydrogenMass;

                            //Console.WriteLine(sn1Carbon + ":" + sn1Double + "-" + sn2Carbon + ":" + sn2Double + 
                            //    " " + nl_SN1 + " " + nl_SN2);

                            var query = new List<Peak>
                            {
                                new Peak() { Mz = nl_SN1, Intensity = 5 },
                                new Peak() { Mz = nl_SN2, Intensity = 5 },
                            };
                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);
                            if (foundCount == 2)
                            {
                                var molecule = getDiacylglycerolMoleculeObjAsLevel2("DG", LbmClass.DG, sn1Carbon, sn1Double,
                                sn2Carbon, sn2Double,
                                averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getLipidAnnotaionAsLevel1("DAG", LbmClass.DAG, totalCarbon, totalDoubleBond, score, "");
                    //    candidates.Add(molecule0);
                    //}
                    if (candidates == null || candidates.Count == 0)
                        return null;

                    return returnAnnotationResult("DG", LbmClass.DG, string.Empty, theoreticalMz, adduct,

                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
                else if (adduct.AdductIonName == "[M+Na]+")
                {
                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    /// DG[M+Na]+ is cannot determine acyl chain
                    //for (int sn1Carbon = minSn1Carbon; sn1Carbon <= maxSn1Carbon; sn1Carbon++)
                    //{
                    //    for (int sn1Double = minSn1DoubleBond; sn1Double <= maxSn1DoubleBond; sn1Double++)
                    //    {
                    //        var diagnosticMz = theoreticalMz;// - 22.9892207 + MassDiffDictionary.HydrogenMass; //to choose [M+H]+;

                    //        var sn2Carbon = totalCarbon - sn1Carbon;
                    //        var sn2Double = totalDoubleBond - sn1Double;

                    //        var nl_SN1 = diagnosticMz - acylCainMass(sn1Carbon, sn1Double) - H2O + MassDiffDictionary.HydrogenMass;
                    //        var nl_SN2 = diagnosticMz - acylCainMass(sn2Carbon, sn2Double) - H2O + MassDiffDictionary.HydrogenMass;
                    //        var query = new List<Peak>
                    //        {
                    //            new Peak() { Mz = nl_SN1, Intensity = 0.1 },
                    //            new Peak() { Mz = nl_SN2, Intensity = 0.1 },
                    //        };
                    //        var foundCount = 0;
                    //        var averageIntensity = 0.0;
                    //        countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                    //        if (foundCount < 2)
                    //        {
                    //            var foundCount2 = 0;
                    //            var averageIntensity2 = 0.0;
                    //            var diagnosticMzH = theoreticalMz - 22.9892207 + MassDiffDictionary.HydrogenMass;
                    //            var nl_SN1_H = diagnosticMzH - acylCainMass(sn1Carbon, sn1Double) - H2O + MassDiffDictionary.HydrogenMass;
                    //            var nl_SN2_H = diagnosticMzH - acylCainMass(sn2Carbon, sn2Double) - H2O + MassDiffDictionary.HydrogenMass;
                    //            var query2 = new List<Peak>
                    //            {
                    //            new Peak() { Mz = nl_SN1_H, Intensity = 0.1 },
                    //            new Peak() { Mz = nl_SN2_H, Intensity = 0.1 },
                    //            };
                    //            countFragmentExistence(spectrum, query2, ms2Tolerance, out foundCount2, out averageIntensity2);
                    //            if (foundCount2 == 2)
                    //            {
                    //                var molecule = getDiacylglycerolMoleculeObjAsLevel2("DG", LbmClass.DG, sn1Carbon, sn1Double,
                    //            sn2Carbon, sn2Double, averageIntensity2);
                    //                candidates.Add(molecule);
                    //            }
                    //        }
                    //        else if (foundCount == 2)
                    //        {
                    //            var molecule = getDiacylglycerolMoleculeObjAsLevel2("DG", LbmClass.DG, sn1Carbon, sn1Double,
                    //            sn2Carbon, sn2Double,
                    //            averageIntensity);
                    //            candidates.Add(molecule);
                    //        }
                    //    }
                    //}
                    ////if (candidates == null || candidates.Count == 0)
                    ////{
                    ////    var score = 0;
                    ////    var molecule0 = getLipidAnnotaionAsLevel1("DAG", LbmClass.DAG, totalCarbon, totalDoubleBond, score, "");
                    ////    candidates.Add(molecule0);
                    ////}

                    //if (candidates == null || candidates.Count == 0)
                    //    return null;

                    return returnAnnotationResult("DG", LbmClass.DG, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);

                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfMag(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PS 46:6, totalCarbon = 46 and totalDoubleBond = 6
           int minSn1Carbon, int maxSn1Carbon, int minSn1DoubleBond, int maxSn1DoubleBond,
           AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSn1Carbon > totalCarbon) maxSn1Carbon = totalCarbon;
            if (maxSn1DoubleBond > totalDoubleBond) maxSn1DoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // seek -17.026549 (NH3)
                    var threshold = 5;
                    var diagnosticMz1 = theoreticalMz - 17.026549;
                    // seek dehydroxy
                    var diagnosticMz2 = diagnosticMz1 - H2O;  
                   // var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);
                    // if (isClassIon1Found !=true || isClassIon2Found != true) return null;
                    if (isClassIon2Found != true) return null;

                    var candidates = new List<LipidMolecule>();
                    //var averageIntensity = 0.0;
                    //var molecule = getSingleacylchainMoleculeObjAsLevel2("MAG", LbmClass.MAG, totalCarbon, totalDoubleBond,
                    //               averageIntensity);
                    //candidates.Add(molecule);

                    return returnAnnotationResult("MG", LbmClass.MG, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfLysopc(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // 
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {
                    if (totalCarbon > 28) return null; //  currently carbon > 28 is recognized as EtherPC
                    // seek 184.07332 (C5H15NO4P)
                    var threshold = 5.0;
                    var diagnosticMz = 184.07332;
                    var diagnosticMz2 = 104.106990;  
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIon1Found != true) return null;
                    //
                    var candidates = new List<LipidMolecule>();
                    var chainSuffix = "";
                    var diagnosticMzExist = 0.0;
                    var diagnosticMzIntensity = 0.0;
                    var diagnosticMzExist2 = 0.0;
                    var diagnosticMzIntensity2 = 0.0;

                    for (int i = 0; i < spectrum.Count; i++)
                    {
                        var mz = spectrum[i][0];
                        var intensity = spectrum[i][1]; 

                        if (intensity > threshold && Math.Abs(mz - diagnosticMz) < ms2Tolerance)
                        {
                            diagnosticMzExist = mz;
                            diagnosticMzIntensity = intensity;
                        }
                        else if (intensity > threshold && Math.Abs(mz - diagnosticMz2) < ms2Tolerance)
                        {
                            diagnosticMzExist2 = mz;
                            diagnosticMzIntensity2 = intensity;
                        }
                    };

                    if (diagnosticMzIntensity2 / diagnosticMzIntensity > 0.3) //
                    {
                        chainSuffix = "/0:0";
                    }

                    var score = 0.0;
                    if (totalCarbon < 30) score = score + 1.0;
                    var molecule = getSingleacylchainwithsuffixMoleculeObjAsLevel2("LPC", LbmClass.LPC, totalCarbon, totalDoubleBond,
                    score, chainSuffix);
                    candidates.Add(molecule);

                    return returnAnnotationResult("LPC", LbmClass.LPC, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 1);

                }
                else if (adduct.AdductIonName == "[M+Na]+")
                {
                    if (totalCarbon > 28) return null; //  currently carbon > 28 is recognized as EtherPC
                    // seek PreCursor - 59 (C3H9N)
                    var threshold = 10.0;
                    var diagnosticMz = theoreticalMz - 59.072951;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;
                    // seek 104.1070 (C5H14NO) maybe not found
                    //var threshold2 = 1.0;
                    //var diagnosticMz2 = 104.1070;

                    //
                    var candidates = new List<LipidMolecule>();
                    var score = 0.0;
                    if (totalCarbon < 30) score = score + 1.0;
                    var molecule = getSingleacylchainMoleculeObjAsLevel2("LPC", LbmClass.LPC, totalCarbon, totalDoubleBond,
                    score);
                    candidates.Add(molecule);

                    return returnAnnotationResult("LPC", LbmClass.LPC, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    if (totalCarbon > 28) return null; //  currently carbon > 28 is recognized as EtherPC

                    // seek [M-CH3]-
                    var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - 74.036779433 : theoreticalMz - 60.021129369;
                    var diagnosticMz2 = fattyacidProductIon(totalCarbon, totalDoubleBond);
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);
                    if (isClassIon1Found != true || isClassIon2Found != true) return null;

                    //
                    var candidates = new List<LipidMolecule>();
                    //var score = 0.0;
                    //if (totalCarbon < 30) score = score + 1.0;
                    //var molecule = getSingleacylchainMoleculeObjAsLevel2("LPC", LbmClass.LPC, totalCarbon, totalDoubleBond,
                    //score);
                    //candidates.Add(molecule);

                    return returnAnnotationResult("LPC", LbmClass.LPC, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 1);

                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfLysope(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // 
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {
                    if (totalCarbon > 28) return null; //  currently carbon > 28 is recognized as EtherPE

                    // seek PreCursor -141(C2H8NO4P)
                    var threshold = 50.0;
                    var diagnosticMz = theoreticalMz - 141.019094;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIon1Found == false) return null;
                    // reject EtherPE 
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1alkyl = (MassDiffDictionary.CarbonMass * sn1Carbon)
                                        + (MassDiffDictionary.HydrogenMass * ((sn1Carbon * 2) - (sn1Double * 2) + 1));//sn1(ether)

                            var NL_sn1 = diagnosticMz - sn1alkyl + Proton;
                            var sn1_rearrange = sn1alkyl + MassDiffDictionary.HydrogenMass * 2 + 139.00290;//sn1(ether) + C2H5NO4P + proton 

                            var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, NL_sn1, threshold);
                            var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, sn1_rearrange, threshold);
                            if (isClassIon2Found == true || isClassIon3Found == true) return null;
                        };
                    }


                    var candidates = new List<LipidMolecule>();
                    if (totalCarbon > 30)
                    {
                        return returnAnnotationResult("PE", LbmClass.EtherPE, "e", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond + 1, 0, candidates, 1);
                    }
                    else
                    {
                        return returnAnnotationResult("LPE", LbmClass.LPE, "", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 1);
                    }


                }
                else if (adduct.AdductIonName == "[M+Na]+")
                {
                    // seek PreCursor -141(C2H8NO4P)
                    var threshold = 10.0;
                    var diagnosticMz = theoreticalMz - 141.019094;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // reject EtherPE 
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1alkyl = (MassDiffDictionary.CarbonMass * sn1Carbon)
                                        + (MassDiffDictionary.HydrogenMass * ((sn1Carbon * 2) - (sn1Double * 2) + 1));//sn1(ether)

                            var NL_sn1 = diagnosticMz - sn1alkyl + Proton;
                            var sn1_rearrange = sn1alkyl + 139.00290 + MassDiffDictionary.HydrogenMass * 2;//sn1(ether) + C2H5NO4P + proton 

                            var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, NL_sn1, threshold);
                            var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, sn1_rearrange, threshold);
                            if (isClassIon2Found == true || isClassIon3Found == true) return null;
                        };
                    }

                    //
                    var candidates = new List<LipidMolecule>();
                    //var score = 0.0;
                    //if (totalCarbon < 30) score = score + 1.0;
                    //var molecule = getSingleacylchainMoleculeObjAsLevel2("LPE", LbmClass.LPE, totalCarbon, totalDoubleBond,
                    //score);
                    //candidates.Add(molecule);
                    if (totalCarbon > 30)
                    {
                        return returnAnnotationResult("PE", LbmClass.EtherPE, "e", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond + 1, 0, candidates, 2);
                    }
                    else
                    {
                        return returnAnnotationResult("LPE", LbmClass.LPE, "", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 1);
                    }
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    if (totalCarbon > 28) return null; //  currently carbon > 28 is recognized as EtherPE
                    // seek PreCursor -197(C5H12NO5P)
                    var threshold = 10.0;
                    var diagnosticMz = theoreticalMz - 197.04475958;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;
                    //
                    var candidates = new List<LipidMolecule>();
                    //var score = 0.0;
                    //if (totalCarbon < 30) score = score + 1.0;
                    //var molecule = getSingleacylchainMoleculeObjAsLevel2("LPE", LbmClass.LPE, totalCarbon, totalDoubleBond,
                    //score);
                    //candidates.Add(molecule);
                    return returnAnnotationResult("LPE", LbmClass.LPE, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfEtherpc(ObservableCollection<double[]> spectrum, double ms2Tolerance,
        double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
        int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
        AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {
                    // seek 184.07332 (C5H15NO4P)
                    var threshold = 10.0;
                    var diagnosticMz = 184.07332;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;
                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    //var averageIntensity = 0.0;
                    //var molecule = getSingleacylchainwithsuffixMoleculeObjAsLevel2("PC", LbmClass.EtherPC, totalCarbon,
                    //                totalDoubleBond, averageIntensity, "e");
                    //candidates.Add(molecule);

                    return returnAnnotationResult("PC", LbmClass.EtherPC, "e", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
                else if (adduct.AdductIonName == "[M+Na]+")
                {
                    // seek PreCursor - 59 (C3H9N)
                    var threshold = 10.0;
                    var diagnosticMz = theoreticalMz - 59.072951;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;  // must or not?

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    //var averageIntensity = 0.0;
                    //var molecule = getSingleacylchainwithsuffixMoleculeObjAsLevel2("PC", LbmClass.EtherPC, totalCarbon,
                    //                totalDoubleBond, averageIntensity, "e");
                    //candidates.Add(molecule);

                    return returnAnnotationResult("PC", LbmClass.EtherPC, "e", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }

            }
            else
            {
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // seek [M-CH3]-
                    var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - 74.036779433 : theoreticalMz - 60.021129369;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (!isClassIonFound) return null;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var formateMz = theoreticalMz - 60.021129369;
                        var threshold2 = 30;
                        var isClassIonFound2 = isDiagnosticFragmentExist(spectrum, ms2Tolerance, formateMz, threshold2);
                        if (isClassIonFound2) return null;
                    }

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = fattyacidProductIon(sn1Carbon, sn1Double);
                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);
                            var NL_sn2 = diagnosticMz - sn2 - Proton;
                            var NL_sn2AndWater = NL_sn2 + 18.0105642;

                            var query = new List<Peak> {
                                new Peak() { Mz = sn2, Intensity = 30.0 },
                                //new Peak() { Mz = NL_sn2, Intensity = 0.1 },
                                //new Peak() { Mz = NL_sn2AndWater, Intensity = 0.1 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 1)
                            { // 
                                var molecule = getEtherPhospholipidMoleculeObjAsLevel2("PC", LbmClass.EtherPC, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity, "e");
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (candidates.Count == 0) return null;


                    //if (candidates.Count == 0)
                    //{
                    //    var score = 0.0;
                    //    var molecule0 = getSingleacylchainwithsuffixMoleculeObjAsLevel2("PC", LbmClass.EtherPC, totalCarbon,
                    //                    totalDoubleBond, score, "e");
                    //    candidates.Add(molecule0);
                    //};
                    return returnAnnotationResult("PC", LbmClass.EtherPC, "e", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfEtherpe(ObservableCollection<double[]> spectrum, double ms2Tolerance,
        double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
        int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
        AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {
                    // seek -141.019094261 (C2H8NO4P)
                    var threshold = 10.0;
                    var diagnosticMz = theoreticalMz - 141.019094261;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            if (sn1Double >= 5) continue;
                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;
                            var sn1alkyl = (MassDiffDictionary.CarbonMass * sn1Carbon)
                                        + (MassDiffDictionary.HydrogenMass * ((sn1Carbon * 2) - (sn1Double * 2) + 1));//sn1(carbon chain)

                            var NL_sn1 = diagnosticMz - sn1alkyl + Proton;
                            var sn1_rearrange = sn1alkyl + 139.00290 + MassDiffDictionary.HydrogenMass * 2;//sn1(carbon chain) + C2H5NO4P + proton 

                            //Console.WriteLine(sn1Carbon + ":" + sn1Double + "e/" + sn2Carbon + ":" + sn2Double + " " + NL_sn1 + " " + sn1_rearrange);

                            var query = new List<Peak> {
                                    new Peak() { Mz = NL_sn1, Intensity = 1 },
                                    new Peak() { Mz = sn1_rearrange, Intensity = 5 }
                                };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { // now I set 2 as the correct level

                                var etherSuffix = "e";
                                var sn1Double2 = sn1Double;
                                if (sn1Double > 0)
                                {
                                    sn1Double2 = sn1Double - 1;
                                    etherSuffix = "p";
                                };

                                var molecule = getEtherPhospholipidMoleculeObjAsLevel2("PE", LbmClass.EtherPE, sn1Carbon, sn1Double2,
                                    sn2Carbon, sn2Double, averageIntensity, etherSuffix);
                                candidates.Add(molecule);
                            } 
                            //else {
                            //    var sn2 = acylCainMass(sn2Carbon, sn2Double) - Electron;
                            //    query = new List<Peak> {
                            //        new Peak() { Mz = sn2, Intensity = 0.1 }
                            //    };

                            //    foundCount = 0;
                            //    averageIntensity = 0.0;
                            //    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            //    if (foundCount == 1) {
                            //        var molecule = getEtherPhospholipidMoleculeObjAsLevel2("PE", LbmClass.EtherPE, sn1Carbon, sn1Double,
                            //        sn2Carbon, sn2Double, averageIntensity, "e");
                            //        candidates.Add(molecule);
                            //    }
                            //}
                        }
                    }
                    //if (candidates.Count == 0)
                    //{
                    //    var score = 0.0;
                    //    var molecule0 = getSingleacylchainwithsuffixMoleculeObjAsLevel2("PE", LbmClass.EtherPE, totalCarbon,
                    //                    totalDoubleBond, score, "e");
                    //    candidates.Add(molecule0);
                    //};

                    return returnAnnotationResult("PE", LbmClass.EtherPE, "e", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek C5H11NO5P-
                    var threshold = 5.0;
                    var diagnosticMz = 196.03803;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;
                            if (sn1Carbon >= 24 && sn1Double >= 5) return null;

                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);
                            var NL_sn2 = theoreticalMz - acylCainMass(sn2Carbon, sn2Double) + MassDiffDictionary.HydrogenMass;

                            var query = new List<Peak> {
                            new Peak() { Mz = sn2, Intensity = 10.0 },
                            new Peak() { Mz = NL_sn2, Intensity = 0.1 }
                        };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { // now I set 2 as the correct level
                                var molecule = getEtherPhospholipidMoleculeObjAsLevel2("PE", LbmClass.EtherPE, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity, "e");
                                candidates.Add(molecule);
                            }
                        }
                    }

                    if (candidates.Count == 0) return null;
                    //if (candidates.Count == 0)
                    //{
                    //    var score = 0.0;
                    //    var molecule0 = getSingleacylchainwithsuffixMoleculeObjAsLevel2("PE", LbmClass.EtherPE, totalCarbon,
                    //                    totalDoubleBond, score, "e");
                    //    candidates.Add(molecule0);
                    //};

                    return returnAnnotationResult("PE", LbmClass.EtherPE, "e", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfEtheroxpc(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct, int TotalOxidized, int sn1MaxOxidized, int sn2MaxOxidized)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (sn1MaxOxidized > TotalOxidized) sn1MaxOxidized = TotalOxidized;
            if (sn2MaxOxidized > TotalOxidized) sn2MaxOxidized = TotalOxidized;

            if (adduct.IonMode == IonMode.Negative)
            { //  
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // seek [M-CH3]-
                    var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - 74.036779433 : theoreticalMz - 60.021129369;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // seek [M-H-H2O]-
                    var threshold2 = 5.0;
                    var diagnosticMz2 = theoreticalMz - H2O;
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    // if (isClassIon2Found == false) return null;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var formateMz = theoreticalMz - 60.021129369;
                        var threshold3 = 30;
                        var isClassIonFound2 = isDiagnosticFragmentExist(spectrum, ms2Tolerance, formateMz, threshold3);
                        if (isClassIonFound2) return null;
                    }

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            if (sn1Double > 0)
                            {
                                if ((double)(sn1Carbon / sn1Double) < 3) break;
                            }

                            for (int sn1Oxidized = 0; sn1Oxidized <= sn1MaxOxidized; sn1Oxidized++)
                            {
                                var sn2Carbon = totalCarbon - sn1Carbon;
                                var sn2Double = totalDoubleBond - sn1Double;
                                var sn2Oxidized = TotalOxidized - sn1Oxidized;

                                if (sn2Double > 0)
                                {
                                    if ((double)(sn2Carbon / sn2Double) < 3) break;
                                }

                                // ether chain loss is not found
                                var sn2 = fattyacidProductIon(sn2Carbon, sn2Double) + (MassDiffDictionary.OxygenMass * sn2Oxidized);
                                var sn2_H2Oloss = sn2 - (H2O);
                                var sn2_xH2Oloss = sn2 - (H2O * sn2Oxidized);
                                var nl_sn2 = diagnosticMz - sn2 - Proton;
                                var nl_sn2H2O = nl_sn2 + 18.0105642;

                                var query1 = new List<Peak> {
                                    new Peak() { Mz = nl_sn2, Intensity = 0.1 },
                                };

                                var foundCount1 = 0;
                                var averageIntensity1 = 0.0;
                                countFragmentExistence(spectrum, query1, ms2Tolerance, out foundCount1, out averageIntensity1);

                                if (foundCount1 == 1)
                                {
                                    var query = new List<Peak> {
                                    new Peak() { Mz = sn2, Intensity = 0.1 },
                                    new Peak() { Mz = sn2_H2Oloss, Intensity = 0.1 },
                                    new Peak() { Mz = sn2_xH2Oloss, Intensity = 0.1 },
                                    //new Peak() { Mz = NL_sn2, Intensity = 0.1 },
                                    //new Peak() { Mz = NL_sn2H2O, Intensity = 0.1 }
                                };
                                    var foundCount = 0;
                                    var averageIntensity = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                    if (foundCount >= 1)
                                    { // 
                                        var molecule = getEtherOxPxMoleculeObjAsLevel2("PC", LbmClass.EtherOxPC, sn1Carbon, sn1Double,
                                            sn2Carbon, sn2Double, sn1Oxidized, sn2Oxidized, averageIntensity, "e");
                                        candidates.Add(molecule);
                                    }
                                }
                            }
                        }
                    }
                    if (isClassIon2Found == false && candidates.Count == 0) return null;

                    return returnAnnotationResult("PC", LbmClass.EtherOxPC, "e", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfEtheroxpe(ObservableCollection<double[]> spectrum, double ms2Tolerance,
                double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
                int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
                AdductIon adduct, int TotalOxidized, int sn1MaxOxidized, int sn2MaxOxidized)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (sn1MaxOxidized > TotalOxidized) sn1MaxOxidized = TotalOxidized;
            if (sn2MaxOxidized > TotalOxidized) sn2MaxOxidized = TotalOxidized;

            if (adduct.IonMode == IonMode.Negative)
            { //  
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek C5H11NO5P-
                    var threshold = 5.0;
                    var diagnosticMz = 196.03803;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // seek [M-H-H2O]-
                    var threshold2 = 5.0;
                    var diagnosticMz2 = theoreticalMz - H2O;
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon2Found == false) return null;

                    var threshold3 = 5.0;
                    var diagnosticMz3 = 152.995833871; // seek C3H6O5P- maybe LNAPE
                    var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                    //if (isClassIon3Found == true) return null;


                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            if (sn1Double > 0)
                            {
                                if ((double)(sn1Carbon / sn1Double) < 3) break;
                            }

                            for (int sn1Oxidized = 0; sn1Oxidized <= sn1MaxOxidized; sn1Oxidized++)
                            {
                                var sn2Carbon = totalCarbon - sn1Carbon;
                                var sn2Double = totalDoubleBond - sn1Double;
                                var sn2Oxidized = TotalOxidized - sn1Oxidized;

                                if (sn2Double > 0)
                                {
                                    if ((double)(sn2Carbon / sn2Double) < 3) break;
                                }

                                // ether chain loss is not found
                                var sn2 = fattyacidProductIon(sn2Carbon, sn2Double) + (MassDiffDictionary.OxygenMass * sn2Oxidized);
                                var sn2_H2Oloss = sn2 - (H2O);
                                var sn2_xH2Oloss = sn2 - (H2O * sn2Oxidized);
                                var nl_sn2 = theoreticalMz - sn2 - Proton;
                                var nl_sn2AndWater = nl_sn2 + 18.0105642;

                                var query1 = new List<Peak> {
                                    new Peak() { Mz = nl_sn2, Intensity = 0.1 },
                                };

                                var foundCount1 = 0;
                                var averageIntensity1 = 0.0;
                                countFragmentExistence(spectrum, query1, ms2Tolerance, out foundCount1, out averageIntensity1);

                                if (foundCount1 == 1)
                                {
                                    var query = new List<Peak> {
                                    new Peak() { Mz = sn2, Intensity = 0.1 },
                                    new Peak() { Mz = sn2_H2Oloss, Intensity = 0.1 },
                                    new Peak() { Mz = sn2_xH2Oloss, Intensity = 0.1 },
                                    //new Peak() { Mz = nl_sn2, Intensity = 0.1 },
                                    //new Peak() { Mz = nl_sn2AndWater, Intensity = 0.1 }
                                };

                                    var foundCount = 0;
                                    var averageIntensity = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                    if (foundCount >= 1)
                                    { // 
                                        var molecule = getEtherOxPxMoleculeObjAsLevel2("PE", LbmClass.EtherOxPE, sn1Carbon, sn1Double,
                                            sn2Carbon, sn2Double, sn1Oxidized, sn2Oxidized, averageIntensity, "e");
                                        candidates.Add(molecule);
                                    }
                                }
                            }
                        }
                    }
                    if (candidates.Count == 0) return null;
                    return returnAnnotationResult("PE", LbmClass.EtherOxPE, "e", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }




        public static LipidMolecule JudgeIfEtherlysopc(ObservableCollection<double[]> spectrum, double ms2Tolerance,
    double theoreticalMz, int totalCarbon, int totalDoubleBond, // 
    int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
    AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+") {
                    // seek 184.07332 (C5H15NO4P), 104.10699 (C5H12N+), 124.99982 (C2H5O4P + H+)
                    var threshold = 5.0;
                    var diagnosticMz1 = 184.07332;
                    var diagnosticMz2 = 104.106990;
                    var diagnosticMz3 = 124.99982;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);
                    var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold);
                    if (isClassIon1Found != true || isClassIon2Found != true || isClassIon3Found != true) return null;
                    //
                    var candidates = new List<LipidMolecule>();
                    //var averageIntensity = 0.0;
                    //var molecule = getSingleacylchainwithsuffixMoleculeObjAsLevel2("LPC", LbmClass.EtherLPC, totalCarbon,
                    //                totalDoubleBond, averageIntensity, "e");
                    //candidates.Add(molecule);
                    return returnAnnotationResult("LPC", LbmClass.EtherLPC, "e", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            else {
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-") {
                    // seek [M-CH3]-
                    var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - 74.036779433 : theoreticalMz - 60.021129369;
                    var diagnosticMz2 = diagnosticMz - 89.08461258; //[M-CH3 -C4H11NO]-
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);
                    if (isClassIon1Found != true || isClassIon2Found != true) return null;

                    //
                    var candidates = new List<LipidMolecule>();
                    //var averageIntensity = 0.0;
                    //var molecule = getSingleacylchainwithsuffixMoleculeObjAsLevel2("LPC", LbmClass.EtherLPC, totalCarbon,
                    //                totalDoubleBond, averageIntensity, "e");
                    //candidates.Add(molecule);
                    return returnAnnotationResult("LPC", LbmClass.EtherLPC, "e", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 1);

                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfEtherlysope(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // 
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+") {
                    // seek PreCursor -171([M-C3H10NO5P]+)
                    var threshold = 70.0;
                    var diagnosticMz = theoreticalMz - 171.0291124 - MassDiffDictionary.HydrogenMass;
                    // seek PreCursor -189([M-C3H8NO4P]+)
                    var threshold2 = 5.0;
                    var diagnosticMz2 = diagnosticMz + H2O;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);

                    if (isClassIon1Found == false && isClassIon2Found == false) return null;
                    //

                    var candidates = new List<LipidMolecule>();
                    //var averageIntensity = 0.0;
                    //var molecule = getSingleacylchainwithsuffixMoleculeObjAsLevel2("LPE", LbmClass.EtherLPE, totalCarbon,
                    //                totalDoubleBond, averageIntensity, "e");
                    //candidates.Add(molecule);
                    return returnAnnotationResult("LPE", LbmClass.EtherLPE, "e", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 1);

                }
            }
            else {
                if (adduct.AdductIonName == "[M-H]-") {
                    // seek PreCursor -197(C5H12NO5P-) , -61(C2H8NO-)
                    var threshold = 10.0;
                    var diagnosticMz1 = theoreticalMz - 197.0447624;
                    var diagnosticMz2 = theoreticalMz - 61.052764;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);
                    if (isClassIon1Found == false && isClassIon2Found == false) return null;

                    //
                    var candidates = new List<LipidMolecule>();
                    //var averageIntensity = 0.0;
                    //var molecule = getSingleacylchainwithsuffixMoleculeObjAsLevel2("LPE", LbmClass.EtherLPE, totalCarbon,
                    //               totalDoubleBond, averageIntensity, "e");
                    //candidates.Add(molecule);
                    return returnAnnotationResult("LPE", LbmClass.EtherLPE, "e", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfOxpc(ObservableCollection<double[]> spectrum, double ms2Tolerance,
        double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
        int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
        AdductIon adduct, int TotalOxidized, int sn1MaxOxidized, int sn2MaxOxidized) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (sn1MaxOxidized > TotalOxidized) sn1MaxOxidized = TotalOxidized;
            if (sn2MaxOxidized > TotalOxidized) sn2MaxOxidized = TotalOxidized;

            if (adduct.IonMode == IonMode.Negative) { //  
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-") {
                    // seek [M-CH3]-
                    var threshold = 5.0;
                    var diagnosticMz = adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - 74.036779433 : theoreticalMz - 60.021129369;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var formateMz = theoreticalMz - 60.021129369;
                        var threshold4 = 30;
                        var isClassIonFound2 = isDiagnosticFragmentExist(spectrum, ms2Tolerance, formateMz, threshold4);
                        if (isClassIonFound2) return null;
                    }

                    // seek [M-H-H2O]-
                    var threshold2 = 5.0;
                    var diagnosticMz2 = theoreticalMz - H2O;
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    // if (isClassIon2Found == false) return null;


                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {
                            for (int sn1Oxidized = 0; sn1Oxidized <= sn1MaxOxidized; sn1Oxidized++) {
                            var sn2Carbon = totalCarbon - sn1Carbon;
                               // if (sn2Carbon < minSnCarbon) { break; }
                                var sn2Double = totalDoubleBond - sn1Double;
                                var sn2Oxidized = TotalOxidized - sn1Oxidized;

                                var sn1 = fattyacidProductIon(sn1Carbon, sn1Double) + (MassDiffDictionary.OxygenMass * sn1Oxidized);
                                var sn1_H2Oloss = sn1 - (H2O);
                                var sn1_xH2Oloss = sn1 - (H2O * Math.Min(sn1Oxidized, 2));
                                var sn2 = fattyacidProductIon(sn2Carbon, sn2Double) + (MassDiffDictionary.OxygenMass * sn2Oxidized);
                                var sn2_H2Oloss = sn2 - (H2O);
                                var sn2_xH2Oloss = sn2 - (H2O * Math.Min(TotalOxidized - sn1Oxidized, 2));

                                var query1 = new List<Peak> {
                                    new Peak() { Mz = sn1, Intensity = 10 },
                                };

                                var foundCount1 = 0;
                                var averageIntensity1 = 0.0;
                                countFragmentExistence(spectrum, query1, ms2Tolerance, out foundCount1, out averageIntensity1);

                                if (foundCount1 == 1) {
                                    var query = new List<Peak> {
                                    new Peak() { Mz = sn2, Intensity = 0.1 },
                                    new Peak() { Mz = sn2_H2Oloss, Intensity = 0.1 },
                                    new Peak() { Mz = sn2_xH2Oloss, Intensity = 0.1 }
                                };

                                    var foundCount2 = 0;
                                    var averageIntensity2 = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount2, out averageIntensity2);

                                    if (foundCount2 >= 1)  // 4 or 5
                                    { // 
                                        var molecule = getOxydizedPhospholipidMoleculeObjAsLevel2("PC", LbmClass.OxPC, sn1Carbon, sn1Double,
                                            sn2Carbon, sn2Double, sn1Oxidized, sn2Oxidized, averageIntensity2);
                                        candidates.Add(molecule);
                                    }
                                }
                            }
                        }
                    }
                    if (isClassIon2Found == false && candidates.Count == 0) return null;

                    //var score = 0;
                    //var molecule0 = getOxydizedPhospholipidMoleculeObjAsLevel1("OxPC", LbmClass.OxPC, totalCarbon, totalDoubleBond, TotalOxidized, score);
                    //candidates.Add(molecule0);

                    return returnAnnotationResult("PC", LbmClass.OxPC, "", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfOxpe(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct, int TotalOxidized, int sn1MaxOxidized, int sn2MaxOxidized)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (sn1MaxOxidized > TotalOxidized) sn1MaxOxidized = TotalOxidized;
            if (sn2MaxOxidized > TotalOxidized) sn2MaxOxidized = TotalOxidized;

            if (adduct.IonMode == IonMode.Negative)
            { // 
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek C5H11NO5P-
                    var threshold = 0.1;
                    var diagnosticMz = 196.03803;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // seek [M-H-H2O]-
                    var threshold2 = 5.0;
                    var diagnosticMz2 = theoreticalMz - H2O;
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    //if (isClassIonFound == false && isClassIon2Found == false) return null;


                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            if (sn1Double > 0)
                            {
                                if ((double)(sn1Carbon / sn1Double) < 3) break;
                            }

                            for (int sn1Oxidized = 0; sn1Oxidized <= sn1MaxOxidized; sn1Oxidized++)
                            {
                                var sn2Carbon = totalCarbon - sn1Carbon;
                              //  if (sn2Carbon < minSnCarbon) { break; }
                                var sn2Double = totalDoubleBond - sn1Double;
                                var sn2Oxidized = TotalOxidized - sn1Oxidized;
                                if (sn2Double > 0)
                                {
                                    if ((double)(sn2Carbon / sn2Double) < 3) break;
                                }
                                var sn1 = fattyacidProductIon(sn1Carbon, sn1Double) + (MassDiffDictionary.OxygenMass * sn1Oxidized);
                                var sn1_H2Oloss = sn1 - (H2O);
                                var sn1_xH2Oloss = sn1 - (H2O * Math.Min(sn1Oxidized, 2));
                                var sn2 = fattyacidProductIon(sn2Carbon, sn2Double) + (MassDiffDictionary.OxygenMass * sn2Oxidized);
                                var sn2_H2Oloss = sn2 - (H2O);
                                var sn2_xH2Oloss = sn2 - (H2O * Math.Min(TotalOxidized - sn1Oxidized, 2));

                                var query1 = new List<Peak> {
                                    new Peak() { Mz = sn1, Intensity = 10 },
                                };

                                var foundCount1 = 0;
                                var averageIntensity1 = 0.0;
                                countFragmentExistence(spectrum, query1, ms2Tolerance, out foundCount1, out averageIntensity1);

                                if (foundCount1 == 1) {
                                    var query = new List<Peak> {
                                    new Peak() { Mz = sn2, Intensity = 0.1 },
                                    new Peak() { Mz = sn2_H2Oloss, Intensity = 0.1 },
                                    new Peak() { Mz = sn2_xH2Oloss, Intensity = 0.1 }
                                };

                                    var foundCount2 = 0;
                                    var averageIntensity2 = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount2, out averageIntensity2);

                                    if (foundCount2 >= 1)  // 4 or 5
                                    { // 
                                        var molecule = getOxydizedPhospholipidMoleculeObjAsLevel2("PE", LbmClass.OxPE, sn1Carbon, sn1Double,
                                            sn2Carbon, sn2Double, sn1Oxidized, sn2Oxidized, averageIntensity2);
                                        candidates.Add(molecule);
                                    }
                                }

                                //var query = new List<Peak> {
                                //    new Peak() { Mz = sn1, Intensity = 0.1 },
                                //    new Peak() { Mz = sn2, Intensity = 0.1 },
                                //    new Peak() { Mz = sn1_H2Oloss, Intensity = 0.1 },
                                //    new Peak() { Mz = sn2_H2Oloss, Intensity = 0.1 },
                                //    new Peak() { Mz = sn1_xH2Oloss, Intensity = 0.1 },
                                //    new Peak() { Mz = sn2_xH2Oloss, Intensity = 0.1 }
                                //};

                                //var foundCount = 0;
                                //var averageIntensity = 0.0;
                                //countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                //if (foundCount >= 4)
                                //{ // 
                                //    var molecule = getOxydizedPhospholipidMoleculeObjAsLevel2("OxPE", LbmClass.OxPE, sn1Carbon, sn1Double,
                                //        sn2Carbon, sn2Double, sn1Oxidized, sn2Oxidized, averageIntensity);
                                //    candidates.Add(molecule);
                                //}
                            }
                        }
                    }
                    if (candidates.Count == 0) return null;

                    //var score = 0;
                    //var molecule0 = getOxydizedPhospholipidMoleculeObjAsLevel1("OxPE", LbmClass.OxPE, totalCarbon, totalDoubleBond, TotalOxidized, score);
                    //candidates.Add(molecule0);

                    return returnAnnotationResult("PE", LbmClass.OxPE, "", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfOxpg(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct, int TotalOxidized, int sn1MaxOxidized, int sn2MaxOxidized) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (sn1MaxOxidized > TotalOxidized) sn1MaxOxidized = TotalOxidized;
            if (sn2MaxOxidized > TotalOxidized) sn2MaxOxidized = TotalOxidized;

            if (adduct.IonMode == IonMode.Negative) { // 
                if (adduct.AdductIonName == "[M-H]-") {
                    // seek C3H6O5P-
                    var threshold = 0.01;
                    var diagnosticMz = 152.995833871;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    //if (isClassIonFound == false) return null;

                    // seek [M-H-H2O]-
                    var threshold2 = 5.0;
                    var diagnosticMz2 = theoreticalMz - H2O;
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon2Found == false) return null;

                    var threshold1 = 50.0;
                    var diagnosticMzSm1 = theoreticalMz - 74.036779433;
                    var diagnosticMzSm2 = theoreticalMz - 60.021129369;
                    var isClassIonSm1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMzSm1, threshold1);
                    var isClassIonSm2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMzSm2, threshold1);
                    if (isClassIonSm1Found == true || isClassIonSm2Found == true) return null;


                    // from here, acyl level annotation is executed.
                    // because correct MS2 data is not annotated, only calculation is presented
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {
                            for (int sn1Oxidized = 0; sn1Oxidized <= sn1MaxOxidized; sn1Oxidized++) {
                                //int sn1Oxidized = 0;
                                var sn2Carbon = totalCarbon - sn1Carbon;
                                //if (sn2Carbon < minSnCarbon) { break; }
                                var sn2Double = totalDoubleBond - sn1Double;
                                var sn2Oxidized = TotalOxidized - sn1Oxidized;

                                var sn1 = fattyacidProductIon(sn1Carbon, sn1Double) + (MassDiffDictionary.OxygenMass * sn1Oxidized);
                                var sn1_H2Oloss = sn1 - (H2O);
                                var sn1_xH2Oloss = sn1 - (H2O * Math.Min(sn1Oxidized, 2));
                                var sn2 = fattyacidProductIon(sn2Carbon, sn2Double) + (MassDiffDictionary.OxygenMass * sn2Oxidized);
                                var sn2_H2Oloss = sn2 - (H2O);
                                var sn2_xH2Oloss = sn2 - (H2O * Math.Min(TotalOxidized - sn1Oxidized, 2));

                                var query1 = new List<Peak> {
                                    new Peak() { Mz = sn1, Intensity = 10 },
                                };

                                var foundCount1 = 0;
                                var averageIntensity1 = 0.0;
                                countFragmentExistence(spectrum, query1, ms2Tolerance, out foundCount1, out averageIntensity1);

                                if (foundCount1 == 1) {
                                    var query = new List<Peak> {
                                    new Peak() { Mz = sn2, Intensity = 0.1 },
                                    new Peak() { Mz = sn2_H2Oloss, Intensity = 0.1 },
                                    new Peak() { Mz = sn2_xH2Oloss, Intensity = 0.1 }
                                };

                                    var foundCount2 = 0;
                                    var averageIntensity2 = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount2, out averageIntensity2);

                                    if (foundCount2 >= 1)  // 4 or 5
                                    { // 
                                        var molecule = getOxydizedPhospholipidMoleculeObjAsLevel2("PG", LbmClass.OxPG, sn1Carbon, sn1Double,
                                            sn2Carbon, sn2Double, sn1Oxidized, sn2Oxidized, averageIntensity2);
                                        candidates.Add(molecule);
                                    }
                                }

                                //var query = new List<Peak> {
                                //    new Peak() { Mz = sn1, Intensity = 0.1 },
                                //    new Peak() { Mz = sn2, Intensity = 0.1 },
                                //    new Peak() { Mz = sn1_H2Oloss, Intensity = 0.1 },
                                //    new Peak() { Mz = sn2_H2Oloss, Intensity = 0.1 },
                                //    new Peak() { Mz = sn1_xH2Oloss, Intensity = 0.1 },
                                //    new Peak() { Mz = sn2_xH2Oloss, Intensity = 0.1 }
                                //};

                                //var foundCount = 0;
                                //var averageIntensity = 0.0;
                                //countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                //if (foundCount >= 4)
                                //{ // 
                                //    var molecule = getOxydizedPhospholipidMoleculeObjAsLevel2("OxPG", LbmClass.OxPG, sn1Carbon, sn1Double,
                                //        sn2Carbon, sn2Double, sn1Oxidized, sn2Oxidized, averageIntensity);
                                //    candidates.Add(molecule);
                                //}
                                //}
                            }
                        }
                    }

                    //var score = 0;
                    //var molecule0 = getOxydizedPhospholipidMoleculeObjAsLevel1("OxPG", LbmClass.OxPG, totalCarbon, totalDoubleBond, TotalOxidized, score);
                    //candidates.Add(molecule0);
                    if (candidates.Count == 0) return null;
                    return returnAnnotationResult("PG", LbmClass.OxPG, "", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfOxpi(ObservableCollection<double[]> spectrum, double ms2Tolerance,
    double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
    int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
    AdductIon adduct, int TotalOxidized, int sn1MaxOxidized, int sn2MaxOxidized)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (sn1MaxOxidized > TotalOxidized) sn1MaxOxidized = TotalOxidized;
            if (sn2MaxOxidized > TotalOxidized) sn2MaxOxidized = TotalOxidized;

            if (adduct.IonMode == IonMode.Negative)
            { // 
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek 241.01188(C6H10O8P-) and 297.037548(C9H14O9P-)
                    var threshold = 0.01;
                    var diagnosticMz1 = 241.01188 + Electron;
                    var diagnosticMz2 = 297.037548 + Electron;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);
                    if (!isClassIon1Found && !isClassIon2Found) return null;

                    // seek [M-H-H2O]-
                    var threshold2 = 5.0;
                    var diagnosticMz3 = theoreticalMz - H2O;
                    var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold2);
                    //if (isClassIon3Found == false) return null;


                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            for (int sn1Oxidized = 0; sn1Oxidized <= sn1MaxOxidized; sn1Oxidized++) {
                                var sn2Carbon = totalCarbon - sn1Carbon;
                                //if (sn2Carbon < minSnCarbon) { break; }
                                var sn2Double = totalDoubleBond - sn1Double;
                                var sn2Oxidized = TotalOxidized - sn1Oxidized;

                                var sn1 = fattyacidProductIon(sn1Carbon, sn1Double) + (MassDiffDictionary.OxygenMass * sn1Oxidized);
                                var sn1_H2Oloss = sn1 - (H2O);
                                var sn1_xH2Oloss = sn1 - (H2O * Math.Min(sn1Oxidized, 2));
                                var sn2 = fattyacidProductIon(sn2Carbon, sn2Double) + (MassDiffDictionary.OxygenMass * sn2Oxidized);
                                var sn2_H2Oloss = sn2 - (H2O);
                                var sn2_xH2Oloss = sn2 - (H2O * Math.Min(TotalOxidized - sn1Oxidized, 2));

                                var query1 = new List<Peak> {
                                    new Peak() { Mz = sn1, Intensity = 10 },
                                };

                                var foundCount1 = 0;
                                var averageIntensity1 = 0.0;
                                countFragmentExistence(spectrum, query1, ms2Tolerance, out foundCount1, out averageIntensity1);

                                if (foundCount1 == 1) {
                                    var query = new List<Peak> {
                                    new Peak() { Mz = sn2, Intensity = 0.1 },
                                    new Peak() { Mz = sn2_H2Oloss, Intensity = 0.1 },
                                    new Peak() { Mz = sn2_xH2Oloss, Intensity = 0.1 }
                                };

                                    var foundCount2 = 0;
                                    var averageIntensity2 = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount2, out averageIntensity2);

                                    if (foundCount2 >= 1)  // 4 or 5
                                    { // 
                                        var molecule = getOxydizedPhospholipidMoleculeObjAsLevel2("PI", LbmClass.OxPI, sn1Carbon, sn1Double,
                                            sn2Carbon, sn2Double, sn1Oxidized, sn2Oxidized, averageIntensity2);
                                        candidates.Add(molecule);
                                    }
                                }

                                //var query = new List<Peak> {
                                //    new Peak() { Mz = sn1, Intensity = 0.1 },
                                //    new Peak() { Mz = sn2, Intensity = 0.1 },
                                //    new Peak() { Mz = sn1_H2Oloss, Intensity = 0.1 },
                                //    new Peak() { Mz = sn2_H2Oloss, Intensity = 0.1 },
                                //    new Peak() { Mz = sn1_xH2Oloss, Intensity = 0.1 },
                                //    new Peak() { Mz = sn2_xH2Oloss, Intensity = 0.1 }
                                //};

                                //var foundCount = 0;
                                //var averageIntensity = 0.0;
                                //countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                //if (foundCount >= 4)
                                //{ // 
                                //    var molecule = getOxydizedPhospholipidMoleculeObjAsLevel2("OxPI", LbmClass.OxPI, sn1Carbon, sn1Double,
                                //        sn2Carbon, sn2Double, sn1Oxidized, sn2Oxidized, averageIntensity);
                                //    candidates.Add(molecule);
                                //}
                            }
                        }
                    }

                    //var score = 0;
                    //var molecule0 = getOxydizedPhospholipidMoleculeObjAsLevel1("OxPI", LbmClass.OxPI, totalCarbon, totalDoubleBond, TotalOxidized, score);
                    //candidates.Add(molecule0);
                    if (candidates.Count == 0) return null;
                    return returnAnnotationResult("PI", LbmClass.OxPI, "", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfOxps(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct, int TotalOxidized, int sn1MaxOxidized, int sn2MaxOxidized)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (sn1MaxOxidized > TotalOxidized) sn1MaxOxidized = TotalOxidized;
            if (sn2MaxOxidized > TotalOxidized) sn2MaxOxidized = TotalOxidized;

            if (adduct.IonMode == IonMode.Negative)
            { // 
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek C3H5NO2 loss
                    var threshold = 1.0;
                    var diagnosticMz = theoreticalMz - 87.032029;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // seek [M-H-H2O]-
                    var threshold2 = 5.0;
                    var diagnosticMz2 = theoreticalMz - H2O;
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    //if (isClassIon2Found == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            for (int sn1Oxidized = 0; sn1Oxidized <= sn1MaxOxidized; sn1Oxidized++) {
                            var sn2Carbon = totalCarbon - sn1Carbon;
                               // if (sn2Carbon < minSnCarbon) { break; }
                                var sn2Double = totalDoubleBond - sn1Double;
                                var sn2Oxidized = TotalOxidized - sn1Oxidized;

                                var sn1 = fattyacidProductIon(sn1Carbon, sn1Double) + (MassDiffDictionary.OxygenMass * sn1Oxidized);
                                var sn1_H2Oloss = sn1 - (H2O);
                                var sn1_xH2Oloss = sn1 - (H2O * Math.Min(sn1Oxidized, 2));
                                var sn2 = fattyacidProductIon(sn2Carbon, sn2Double) + (MassDiffDictionary.OxygenMass * sn2Oxidized);
                                var sn2_H2Oloss = sn2 - (H2O);
                                var sn2_xH2Oloss = sn2 - (H2O * Math.Min(TotalOxidized - sn1Oxidized, 2));

                                var query1 = new List<Peak> {
                                    new Peak() { Mz = sn1, Intensity = 10 },
                                };

                                var foundCount1 = 0;
                                var averageIntensity1 = 0.0;
                                countFragmentExistence(spectrum, query1, ms2Tolerance, out foundCount1, out averageIntensity1);

                                if (foundCount1 == 1) {
                                    var query = new List<Peak> {
                                    new Peak() { Mz = sn2, Intensity = 0.1 },
                                    new Peak() { Mz = sn2_H2Oloss, Intensity = 0.1 },
                                    new Peak() { Mz = sn2_xH2Oloss, Intensity = 0.1 }
                                };

                                    var foundCount2 = 0;
                                    var averageIntensity2 = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount2, out averageIntensity2);

                                    if (foundCount2 >= 1)  // 4 or 5
                                    { // 
                                        var molecule = getOxydizedPhospholipidMoleculeObjAsLevel2("PS", LbmClass.OxPS, sn1Carbon, sn1Double,
                                            sn2Carbon, sn2Double, sn1Oxidized, sn2Oxidized, averageIntensity2);
                                        candidates.Add(molecule);
                                    }
                                }

                                //var query = new List<Peak> {
                                //    new Peak() { Mz = sn1, Intensity = 0.1 },
                                //    new Peak() { Mz = sn2, Intensity = 0.1 },
                                //    new Peak() { Mz = sn1_H2Oloss, Intensity = 0.1 },
                                //    new Peak() { Mz = sn2_H2Oloss, Intensity = 0.1 },
                                //    new Peak() { Mz = sn1_xH2Oloss, Intensity = 0.1 },
                                //    new Peak() { Mz = sn2_xH2Oloss, Intensity = 0.1 }
                                //};

                                //var foundCount = 0;
                                //var averageIntensity = 0.0;
                                //countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                //if (foundCount >= 4)
                                //{ // 
                                //    var molecule = getOxydizedPhospholipidMoleculeObjAsLevel2("OxPS", LbmClass.OxPS, sn1Carbon, sn1Double,
                                //        sn2Carbon, sn2Double, sn1Oxidized, sn2Oxidized, averageIntensity);
                                //    candidates.Add(molecule);
                                //}
                            }
                        }
                    }

                    //var score = 0;
                    //var molecule0 = getOxydizedPhospholipidMoleculeObjAsLevel1("OxPS", LbmClass.OxPS, totalCarbon, totalDoubleBond, TotalOxidized, score);
                    //candidates.Add(molecule0);
                    if (candidates.Count == 0) return null;
                    return returnAnnotationResult("PS", LbmClass.OxPS, "", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }



        public static LipidMolecule JudgeIfMgdg(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // seek [M+H-C6H12O6]+
                    var threshold = 10;
                    var diagnosticMz = theoreticalMz - (MassDiffDictionary.NitrogenMass + MassDiffDictionary.HydrogenMass * 3)
                        - (12 * 6 + MassDiffDictionary.HydrogenMass * 12 + MassDiffDictionary.OxygenMass * 6);
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var nl_SN1 = theoreticalMz - acylCainMass(sn1Carbon, sn1Double) - 179.05611 - 17.026549;
                            var nl_SN2 = theoreticalMz - acylCainMass(sn2Carbon, sn2Double) - 179.05611 - 17.026549;

                            var query = new List<Peak> {
                                new Peak() { Mz = nl_SN1, Intensity = 10 },
                                new Peak() { Mz = nl_SN2, Intensity = 10 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("MGDG", LbmClass.MGDG, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    return returnAnnotationResult("MGDG", LbmClass.MGDG, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);

                }
                if (adduct.AdductIonName == "[M+Na]+")
                {
                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var nl_SN1 = theoreticalMz - acylCainMass(sn1Carbon, sn1Double) - H2O + Proton;
                            var nl_SN2 = theoreticalMz - acylCainMass(sn2Carbon, sn2Double) - H2O + Proton;

                            var query = new List<Peak> {
                                new Peak() { Mz = nl_SN1, Intensity = 0.1 },
                                new Peak() { Mz = nl_SN2, Intensity = 0.1 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            {
                                //reject DGDG
                                var threshold = 0.1;
                                var dgdgFrg = nl_SN1 - 162.05282; // one Hex loss
                                var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, dgdgFrg, threshold);
                                if (isClassIonFound == true) return null;

                                var molecule = getPhospholipidMoleculeObjAsLevel2("MGDG", LbmClass.MGDG, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }

                    return returnAnnotationResult("MGDG", LbmClass.MGDG, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);

                }
            }
            else
            {
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    //var threshold = 1.0;
                    var diagnosticMz = adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - 60.02167792 : theoreticalMz - 46.00602785;
                    // seek -H2O -Hex(-C6H10O5)
                    var threshold1 = 0.1;
                    var diagnosticMz1 = diagnosticMz - 162.052833;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    //if (isClassIonFound == false) return null;


                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = fattyacidProductIon(sn1Carbon, sn1Double);
                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);

                            var query = new List<Peak> {
                            new Peak() { Mz = sn1, Intensity = 5.0 },
                            new Peak() { Mz = sn2, Intensity = 5.0 }
                        };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("MGDG", LbmClass.MGDG, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (isClassIonFound == false && candidates.Count == 0) return null;

                    return returnAnnotationResult("MGDG", LbmClass.MGDG, "", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfDgdg(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    // exclude unknown element
                    var isPeakFound = isPeakFoundWithACritetion(spectrum, theoreticalMz - 202, theoreticalMz - 200, 50.00);
                    if (isPeakFound) return null;

                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                           // if (sn2Carbon < minSnCarbon) { break; }
                            var sn2Double = totalDoubleBond - sn1Double;
                            if (sn1Carbon <= 10 || sn2Carbon <= 10) return null;
                            // 2 x Hex loss
                            var nl_SN1 = theoreticalMz - acylCainMass(sn1Carbon, sn1Double) - 341.108935 - 17.026549;
                            var nl_SN2 = theoreticalMz - acylCainMass(sn2Carbon, sn2Double) - 341.108935 - 17.026549;
                            var nl_SN1_H2O = nl_SN1 - H2O;
                            var nl_SN2_H2O = nl_SN2 - H2O;

                            var query = new List<Peak> {
                                new Peak() { Mz = nl_SN1, Intensity = 1 },
                                new Peak() { Mz = nl_SN2, Intensity = 1 },
                                new Peak() { Mz = nl_SN1_H2O, Intensity = 1 },
                                new Peak() { Mz = nl_SN2_H2O, Intensity = 1 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2)
                            { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("DGDG", LbmClass.DGDG, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (candidates.Count == 0) return null; 
                    return returnAnnotationResult("DGDG", LbmClass.DGDG, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);

                }
                if (adduct.AdductIonName == "[M+Na]+")
                {
                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var nl_SN1 = theoreticalMz - acylCainMass(sn1Carbon, sn1Double) - H2O + Proton;
                            var nl_SN2 = theoreticalMz - acylCainMass(sn2Carbon, sn2Double) - H2O + Proton;

                            var query = new List<Peak> {
                                new Peak() { Mz = nl_SN1, Intensity = 0.1 },
                                new Peak() { Mz = nl_SN2, Intensity = 0.1 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2)
                            { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("DGDG", LbmClass.DGDG, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }

                    return returnAnnotationResult("DGDG", LbmClass.DGDG, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);

                }
            }
            else
            {
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // seek [M-H]-
                    var threshold = 5.0;
                    var diagnosticMz = adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - 60.02167792 : theoreticalMz - 46.00602785;
                    var diagnosticMz2 = 379.12459; // C15H23O11
                    var diagnosticMz3 = 397.13515; // C15H25O12
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);
                    var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold);
                    if (isClassIonFound == !true || isClassIon2Found == !true || isClassIon3Found == !true) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = fattyacidProductIon(sn1Carbon, sn1Double);
                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);

                            var query = new List<Peak> {
                            new Peak() { Mz = sn1, Intensity = 5.0 },
                            new Peak() { Mz = sn2, Intensity = 5.0 }
                        };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("DGDG", LbmClass.DGDG, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }

                    return returnAnnotationResult("DGDG", LbmClass.DGDG, "", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfEthermgdg(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // seek [M+H-C6H12O6]+
                    var threshold = 5;
                    var diagnosticMz = theoreticalMz - (MassDiffDictionary.NitrogenMass + MassDiffDictionary.HydrogenMass * 3)
                        - (12 * 6 + MassDiffDictionary.HydrogenMass * 12 + MassDiffDictionary.OxygenMass * 6);
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;
                            if (sn1Carbon >= 26 && sn1Double >= 4) return null;
                            if (sn1Double >= 5) continue;


                            //var sn1alkyl = (MassDiffDictionary.CarbonMass * sn1Carbon)
                            //+ (MassDiffDictionary.HydrogenMass * ((sn1Carbon * 2) - (sn1Double * 2) + 1));//sn1(ether (not containing oxygen))

                            //var nl_SN1 = diagnosticMz - sn1alkyl + Proton;

                            var sn2Dmag = 12 * (sn2Carbon + 3) + MassDiffDictionary.HydrogenMass * ((sn2Carbon * 2) - (sn2Double * 2) - 1 + 5) + 3 * MassDiffDictionary.OxygenMass + Proton;

                            var query = new List<Peak> {
                                new Peak() { Mz = sn2Dmag, Intensity = 30 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 1)
                            { // 
                                var molecule = getEtherPhospholipidMoleculeObjAsLevel2("MGDG", LbmClass.EtherMGDG, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity, "e");
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (candidates.Count == 0) return null;
                    return returnAnnotationResult("MGDG", LbmClass.EtherMGDG, "e", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);

                }
                if (adduct.AdductIonName == "[M+Na]+")
                {
                    var threshold = 1;
                    var diagnosticMz = theoreticalMz - 202.04533; // - Hex and Na
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            if (sn1Carbon >= 26 && sn1Double >= 4) return null;

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1alkyl = (MassDiffDictionary.CarbonMass * sn1Carbon)
                            + (MassDiffDictionary.HydrogenMass * ((sn1Carbon * 2) - (sn1Double * 2) + 1));//sn1(ether (not containing oxygen))
                            var nl_SN1 = diagnosticMz - sn1alkyl + Proton;

                            var query = new List<Peak> {
                                new Peak() { Mz = nl_SN1, Intensity = 10 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 1)
                            { // 
                                var molecule = getEtherPhospholipidMoleculeObjAsLevel2("MGDG", LbmClass.EtherMGDG, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity, "e");
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (candidates.Count == 0) return null;
                    return returnAnnotationResult("MGDG", LbmClass.EtherMGDG, "e", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // seek [M-H]-
                    var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - 60.02167792 : theoreticalMz - 46.00602785;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // 
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            if (sn1Carbon >= 26 && sn1Double >= 4) return null;
                            if (sn1Double >= 5) continue;

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);
                            var NL_sn2 = 12 * (sn1Carbon + 3 + 6) + MassDiffDictionary.HydrogenMass * (2 * (sn1Carbon + 3 - sn1Double) + 11) + MassDiffDictionary.OxygenMass * 8;

                            var query = new List<Peak> {
                            new Peak() { Mz = sn2, Intensity = 10.0 },
                            new Peak() { Mz = NL_sn2, Intensity = 5.0 }
                        };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { //
                                var molecule = getEtherPhospholipidMoleculeObjAsLevel2("MGDG", LbmClass.EtherMGDG, sn1Carbon, sn1Double,
                                   sn2Carbon, sn2Double, averageIntensity, "e");
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (candidates.Count == 0) return null;
                    
                    return returnAnnotationResult("MGDG", LbmClass.EtherMGDG, "e", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfEtherDAG(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+") {
                   
                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        if (sn1Carbon < 8) continue;
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {
                            if (sn1Double > 3) continue;
                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;
                            if (sn2Double >= 7) continue;
                            if (sn2Carbon <= 10) continue;
                            if (sn1Carbon <= 10) continue;
                            if (sn1Double == 19 && sn1Double == 2) continue;

                            var nl_SN1 = theoreticalMz - acylCainMass(sn1Carbon, sn1Double) + MassDiffDictionary.OxygenMass - MassDiffDictionary.NitrogenMass - H2O - 4.0 * MassDiffDictionary.HydrogenMass;

                            var query = new List<Peak> {
                                new Peak() { Mz = nl_SN1, Intensity = 80 },
                            };

                            //Console.WriteLine("Molecule {0}, Diagnostic m/z {1}", "DAG " + sn1Carbon + ":" + sn1Double + "e/" + sn2Carbon + ":" + sn2Double, nl_SN1);


                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 1) { // 
                                var molecule = getEtherPhospholipidMoleculeObjAsLevel2("DG", LbmClass.EtherDG, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity, "e");
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (candidates.Count == 0) return null;
                    return returnAnnotationResult("DG", LbmClass.EtherDG, "e", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfEtherdgdg(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+") // not found in lipidDbProject-Pos
                {
                    // seek -17.026549 (NH3)
                    var diagnosticMz = theoreticalMz - 17.026549;

                    // seek [M -C12H21O11 +H] (-2Hex as 341.10838)  
                    var threshold = 5;
                    var diagnosticMz2 = diagnosticMz - (12 * 12 + MassDiffDictionary.HydrogenMass * 21 + MassDiffDictionary.OxygenMass * 11) + Proton;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);
                    if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        if (sn1Carbon < 10) continue;
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {
                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;
                            if (sn2Carbon < 10) continue;

                            var sn1alkyl = (MassDiffDictionary.CarbonMass * sn1Carbon)
                                + (MassDiffDictionary.HydrogenMass * ((sn1Carbon * 2) - (sn1Double * 2) + 1));//sn1(ether (not containing oxygen))

                            var nl_SN1 = diagnosticMz - sn1alkyl + Proton;

                            var query = new List<Peak> {
                                new Peak() { Mz = nl_SN1, Intensity = 10 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 1) { // 
                                var molecule = getEtherPhospholipidMoleculeObjAsLevel2("DGDG", LbmClass.EtherDGDG, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity, "e");
                                candidates.Add(molecule);
                            }
                        }
                    }
                    return returnAnnotationResult("DGDG", LbmClass.EtherDGDG, "e", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 2);

                }
                if (adduct.AdductIonName == "[M+Na]+") // not found in lipidDbProject-Pos
                {
                    var threshold = 1;
                    var diagnosticMz = theoreticalMz - 341.10838 - 22.9892207 + Proton; // - 2Hex and Na
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        if (sn1Carbon < 8) continue;
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {
                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            if (sn2Carbon < 8) continue;

                            var sn1alkyl = (MassDiffDictionary.CarbonMass * sn1Carbon)
                            + (MassDiffDictionary.HydrogenMass * ((sn1Carbon * 2) - (sn1Double * 2) + 1));//sn1(ether (not containing oxygen))
                            var nl_SN1 = diagnosticMz - sn1alkyl + Proton;

                            var query = new List<Peak> {
                                new Peak() { Mz = nl_SN1, Intensity = 10 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 1) { // 
                                var molecule = getEtherPhospholipidMoleculeObjAsLevel2("DGDG", LbmClass.EtherDGDG, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity, "e");
                                candidates.Add(molecule);
                            }
                        }
                    }
                    return returnAnnotationResult("DGDG", LbmClass.EtherDGDG, "e", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else {
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-") {
                    // seek [M-H]-  // not found in lipidDbProject-Neg
                    var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - 60.02167792 : theoreticalMz - 46.00602785;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // 
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        if (sn1Carbon < 10) continue;
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;
                            if (sn2Carbon < 10) continue;

                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);
                            var NL_sn2 = 12 * (sn1Carbon + 3 + 12) + MassDiffDictionary.HydrogenMass * (2 * (sn1Carbon + 3 - sn1Double) + 21) + MassDiffDictionary.OxygenMass * 13;

                            var query = new List<Peak> {
                                new Peak() { Mz = sn2, Intensity = 10.0 },
                                new Peak() { Mz = NL_sn2, Intensity = 5.0 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2) { //
                                var molecule = getEtherPhospholipidMoleculeObjAsLevel2("DGDG", LbmClass.EtherDGDG, sn1Carbon, sn1Double,
                                   sn2Carbon, sn2Double, averageIntensity, "e");
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (candidates.Count == 0) return null;
                    return returnAnnotationResult("DGDG", LbmClass.EtherDGDG, "e", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }


        public static LipidMolecule JudgeIfPhosphatidicacid(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            { //negative ion mode only
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek C3H6O5P-
                    var threshold = 1.0;
                    var diagnosticMz = 152.99583;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = fattyacidProductIon(sn1Carbon, sn1Double);
                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);
                            var nl_SN1 = theoreticalMz - acylCainMass(sn1Carbon, sn1Double) + MassDiffDictionary.HydrogenMass;
                            var nl_SN1_H2O = nl_SN1 - H2O;

                            var nl_SN2 = theoreticalMz - acylCainMass(sn2Carbon, sn2Double) + MassDiffDictionary.HydrogenMass;
                            var nl_NS2_H2O = nl_SN2 - H2O;

                            var query = new List<Peak> {
                                new Peak() { Mz = sn1, Intensity = 0.01 },
                                new Peak() { Mz = sn2, Intensity = 0.01 },
                                //new Peak() { Mz = nl_SN1, Intensity = 0.01 },
                                //new Peak() { Mz = nl_SN1_H2O, Intensity = 0.01 },
                                //new Peak() { Mz = nl_SN2, Intensity = 0.01 },
                                //new Peak() { Mz = nl_NS2_H2O, Intensity = 0.01 }
                        };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { 
                                var molecule = getPhospholipidMoleculeObjAsLevel2("PA", LbmClass.PA, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                            //else
                            //{
                            //    var score = 0;
                            //    var molecule = getLipidAnnotaionAsLevel1("PA", LbmClass.PA, totalCarbon, totalDoubleBond, score, "");
                            //    candidates.Add(molecule);

                            //}
                        }
                    }

                    if (candidates.Count == 0) return null;

                    return returnAnnotationResult("PA", LbmClass.PA, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfLysopa(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            { //negative ion mode only
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek C3H6O5P-
                    var threshold = 1.0;
                    var diagnosticMz = 152.99583;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // seek FA- fragment 
                    //var threshold2 = 10;
                    //var diagnosticMz2 = fattyacidProductIon(totalCarbon,totalDoubleBond);
                    //var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    //if (isClassIon2Found == true) return null;

                    var query = new List<Peak> {
                                        new Peak() { Mz = 255.2329539, Intensity = 5 },    // 16:0
                                        new Peak() { Mz = 283.264254, Intensity = 5 },     // 18:0
                                        new Peak() { Mz = 281.2486039, Intensity = 5 },    // 18:1 
                                        new Peak() { Mz = 279.2329539, Intensity = 5 },     // 18:2
                                        new Peak() { Mz = 277.2173038, Intensity = 5 },     // 18:3,
                                        new Peak() { Mz = 303.2329539, Intensity = 5 },     // 20:4,
                                        new Peak() { Mz = 327.2329539, Intensity = 5 },     // 22:6,
                                    };

                    var foundCount = 0;
                    var averageIntensity = 0.0;
                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                    if (foundCount >= 1)// Need to consider
                    { // 
                        return null;
                    }

                    //
                    var candidates = new List<LipidMolecule>();
                    //var averageIntensity = 0.0;

                    //var molecule = getSingleacylchainMoleculeObjAsLevel2("LPA", LbmClass.LPA, totalCarbon, totalDoubleBond,
                    //averageIntensity);
                    //candidates.Add(molecule);
                    return returnAnnotationResult("LPA", LbmClass.LPA, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfLysopg(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            { //negative ion mode only
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek C3H6O5P-

                    var diagnosticMz1 = 152.99583;  // seek C3H6O5P-
                    var threshold1 = 1.0;
                    var diagnosticMz2 = fattyacidProductIon(totalCarbon, totalDoubleBond); // seek [FA-H]-
                    var threshold2 = 10.0;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon1Found != true || isClassIon2Found != true) return null;

                    //
                    var candidates = new List<LipidMolecule>();
                    //var averageIntensity = 0.0;

                    //var molecule = getSingleacylchainMoleculeObjAsLevel2("LPG", LbmClass.LPG, totalCarbon, totalDoubleBond,
                    //averageIntensity);
                    //candidates.Add(molecule);
                    return returnAnnotationResult("LPG", LbmClass.LPG, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfLysopi(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            { //negative ion mode only
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek C3H6O5P-

                    var diagnosticMz1 = 241.0118806 + Electron;  // seek C3H6O5P-
                    var threshold1 = 1.0;
                    var diagnosticMz2 = 315.048656; // seek C9H16O10P-
                    var threshold2 = 1.0;
                    var diagnosticMz3 = fattyacidProductIon(totalCarbon, totalDoubleBond); // seek [FA-H]-
                    var threshold3 = 10.0;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                    if (isClassIon1Found != true || isClassIon2Found != true || isClassIon3Found != true) return null;

                    //
                    var candidates = new List<LipidMolecule>();
                    //var averageIntensity = 0.0;

                    //var molecule = getSingleacylchainMoleculeObjAsLevel2("LPI", LbmClass.LPI, totalCarbon, totalDoubleBond,
                    //averageIntensity);
                    //candidates.Add(molecule);
                    return returnAnnotationResult("LPI", LbmClass.LPI, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }
        public static LipidMolecule JudgeIfLysops(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            { //negative ion mode only
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek C3H6O5P-

                    var diagnosticMz1 = 152.99583;  // seek C3H6O5P-
                    var threshold1 = 10.0;
                    var diagnosticMz2 = theoreticalMz - 87.032029; // seek -C3H6NO2-H
                    var threshold2 = 5.0;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon1Found != true || isClassIon2Found != true) return null;

                    //
                    var candidates = new List<LipidMolecule>();
                    //var averageIntensity = 0.0;

                    //var molecule = getSingleacylchainMoleculeObjAsLevel2("LPS", LbmClass.LPS, totalCarbon, totalDoubleBond,
                    //averageIntensity);
                    //candidates.Add(molecule);
                    return returnAnnotationResult("LPS", LbmClass.LPS, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }
        public static LipidMolecule JudgeIfEthertag(ObservableCollection<double[]> spectrum, double ms2Tolerance,
         double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PS 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSn1Carbon, int maxSn1Carbon, int minSn1DoubleBond, int maxSn1DoubleBond,
            int minSn2Carbon, int maxSn2Carbon, int minSn2DoubleBond, int maxSn2DoubleBond,
                AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSn1Carbon > totalCarbon) maxSn1Carbon = totalCarbon;
            if (maxSn1DoubleBond > totalDoubleBond) maxSn1DoubleBond = totalDoubleBond;
            if (maxSn2Carbon > totalCarbon) maxSn2Carbon = totalCarbon;
            if (maxSn2DoubleBond > totalDoubleBond) maxSn2DoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // seek -17.026549 (NH3)
                    var threshold = 1.0;
                    var diagnosticMz = theoreticalMz - 17.026549;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSn1Carbon; sn1Carbon <= maxSn1Carbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSn1DoubleBond; sn1Double <= maxSn1DoubleBond; sn1Double++)
                        {
                            var remainCarbon = totalCarbon - sn1Carbon;
                            var remainDouble = totalDoubleBond - sn1Double;
                            var carbonLimit = Math.Min(remainCarbon, maxSn2Carbon);
                            var doubleLimit = Math.Min(remainDouble, maxSn2DoubleBond);

                            for (int sn2Carbon = minSn2Carbon; sn2Carbon <= carbonLimit; sn2Carbon++)
                            {
                                for (int sn2Double = minSn2DoubleBond; sn2Double <= doubleLimit; sn2Double++)
                                {

                                    var sn3Carbon = totalCarbon - sn1Carbon - sn2Carbon;
                                    var sn3Double = totalDoubleBond - sn1Double - sn2Double;

                                    var sn1alkyl = (MassDiffDictionary.CarbonMass * sn1Carbon)
                                        + (MassDiffDictionary.HydrogenMass * ((sn1Carbon * 2) - (sn1Double * 2) + 1));//sn1(ether chain (not containing oxygen))
                                    var nl_SN1 = diagnosticMz - sn1alkyl - H2O + Proton;
                                    var nl_SN2 = diagnosticMz - acylCainMass(sn2Carbon, sn2Double) - H2O + Proton;
                                    var nl_SN3 = diagnosticMz - acylCainMass(sn3Carbon, sn3Double) - H2O + Proton;
                                    var query = new List<Peak> {
                                        new Peak() { Mz = nl_SN1, Intensity = 5 },
                                        new Peak() { Mz = nl_SN2, Intensity = 5 },
                                        new Peak() { Mz = nl_SN3, Intensity = 5 }
                                    };

                                    var foundCount = 0;
                                    var averageIntensity = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                    if (foundCount == 3)// Need to consider
                                    { // 
                                        var molecule2 = getEthertagMoleculeObjAsLevel2("TG", LbmClass.EtherTG, sn1Carbon, sn1Double,
                                            sn2Carbon, sn2Double, sn3Carbon, sn3Double, averageIntensity);
                                        candidates.Add(molecule2);
                                    }
                                }
                            }

                        }
                    }

                    //var score = 0;
                    //var molecule = getLipidAnnotaionAsLevel1("TAG", LbmClass.EtherTAG, totalCarbon, totalDoubleBond, score, "e");
                    //candidates.Add(molecule);

                    if (candidates == null || candidates.Count == 0) return null;

                    return returnAnnotationResult("TG", LbmClass.EtherTG, "e", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 3);
                }
                else if (adduct.AdductIonName == "[M+Na]+")
                {   
                    // 
                    //var candidates = new List<LipidMolecule>();
                    ////var score = 0;
                    ////var molecule = getLipidAnnotaionAsLevel1("TAG", LbmClass.EtherTAG, totalCarbon, totalDoubleBond, score, "e");
                    ////candidates.Add(molecule);

                    //return returnAnnotationResult("TAG", LbmClass.EtherTAG, "e", theoreticalMz, adduct,
                    //        totalCarbon, totalDoubleBond, 0, candidates);


                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSn1Carbon; sn1Carbon <= maxSn1Carbon; sn1Carbon++) {
                        for (int sn1Double = minSn1DoubleBond; sn1Double <= maxSn1DoubleBond; sn1Double++) {

                            var diagnosticMz = theoreticalMz; // - 22.9892207 + MassDiffDictionary.HydrogenMass; //if want to choose [M+H]+
                            var remainCarbon = totalCarbon - sn1Carbon;
                            var remainDouble = totalDoubleBond - sn1Double;
                            var carbonLimit = Math.Min(remainCarbon, maxSn2Carbon);
                            var doubleLimit = Math.Min(remainDouble, maxSn2DoubleBond);

                            for (int sn2Carbon = minSn2Carbon; sn2Carbon <= carbonLimit; sn2Carbon++) {
                                for (int sn2Double = minSn2DoubleBond; sn2Double <= doubleLimit; sn2Double++) {

                                    var sn3Carbon = totalCarbon - sn1Carbon - sn2Carbon;
                                    var sn3Double = totalDoubleBond - sn1Double - sn2Double;

                                    //var nl_SN1 = diagnosticMz - acylCainMass(sn1Carbon, sn1Double) - H2O + MassDiffDictionary.HydrogenMass;
                                    var nl_SN2 = diagnosticMz - acylCainMass(sn2Carbon, sn2Double) - H2O + MassDiffDictionary.HydrogenMass;
                                    var nl_SN3 = diagnosticMz - acylCainMass(sn3Carbon, sn3Double) - H2O + MassDiffDictionary.HydrogenMass;
                                    var query = new List<Peak> {
                                        //new Peak() { Mz = nl_SN1, Intensity = 0.1 },
                                        new Peak() { Mz = nl_SN2, Intensity = 1 },
                                        new Peak() { Mz = nl_SN3, Intensity = 1 }
                                    };

                                    var foundCount = 0;
                                    var averageIntensity = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                    if (foundCount < 2) {
                                        var diagnosticMzH = theoreticalMz - 22.9892207 + MassDiffDictionary.HydrogenMass;
                                        //var nl_SN1_H = diagnosticMzH - acylCainMass(sn1Carbon, sn1Double) - H2O + MassDiffDictionary.HydrogenMass;
                                        var nl_SN2_H = diagnosticMzH - acylCainMass(sn2Carbon, sn2Double) - H2O + MassDiffDictionary.HydrogenMass;
                                        var nl_SN3_H = diagnosticMzH - acylCainMass(sn3Carbon, sn3Double) - H2O + MassDiffDictionary.HydrogenMass;
                                        var query2 = new List<Peak> {
                                            //new Peak() { Mz = nl_SN1_H, Intensity = 0.1 },
                                            new Peak() { Mz = nl_SN2_H, Intensity = 0.1 },
                                            new Peak() { Mz = nl_SN3_H, Intensity = 0.1 }
                                            };

                                        var foundCount2 = 0;
                                        var averageIntensity2 = 0.0;
                                        countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity2);


                                        if (foundCount2 == 2) {
                                            var molecule = getEthertagMoleculeObjAsLevel2("TG", LbmClass.EtherTG, sn1Carbon, sn1Double,
                                            sn2Carbon, sn2Double, sn3Carbon, sn3Double, averageIntensity2);
                                            candidates.Add(molecule);
                                        }
                                    }
                                    else if (foundCount == 2) { // these three chains must be observed.
                                        var molecule = getEthertagMoleculeObjAsLevel2("TG", LbmClass.EtherTG, sn1Carbon, sn1Double,
                                            sn2Carbon, sn2Double, sn3Carbon, sn3Double, averageIntensity);
                                        candidates.Add(molecule);
                                    }
                                }
                            }
                        }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getLipidAnnotaionAsLevel1("TAG", LbmClass.TAG, totalCarbon, totalDoubleBond, score, "");
                    //    candidates.Add(molecule0);
                    //}
                    if (candidates == null || candidates.Count == 0) return null;
                    return returnAnnotationResult("TG", LbmClass.EtherTG, "e", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 3);

                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfDgts(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {
                    // seek 144.10191 (C7H14NO2+)
                    var threshold = 1.0;
                    var diagnosticMz = 144.10191;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    //if (isClassIonFound == false) return null;
                    // seek 236.1492492 (C10H21NO5H +)
                    var threshold2 = 1.0;
                    var diagnosticMz2 = 236.1492492;
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon2Found == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var nl_SN1 = theoreticalMz - acylCainMass(sn1Carbon, sn1Double) + MassDiffDictionary.HydrogenMass;
                            var nl_SN1_H2O = nl_SN1 - H2O;

                            var nl_SN2 = theoreticalMz - acylCainMass(sn2Carbon, sn2Double) + MassDiffDictionary.HydrogenMass;
                            var nl_NS2_H2O = nl_SN2 - H2O;

                            var query = new List<Peak> {
                                new Peak() { Mz = nl_SN1, Intensity = 0.01 },
                                new Peak() { Mz = nl_SN1_H2O, Intensity = 0.01 },
                                new Peak() { Mz = nl_SN2, Intensity = 0.01 },
                                new Peak() { Mz = nl_NS2_H2O, Intensity = 0.01 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2)
                            { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("DGTS", LbmClass.DGTS, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //var score = 0;
                    //var molecule0 = getLipidAnnotaionAsLevel1("DGTS", LbmClass.DGTS, totalCarbon, totalDoubleBond, score, "");
                    //candidates.Add(molecule0);

                    return returnAnnotationResult("DGTS", LbmClass.DGTS, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else if (adduct.IonMode == IonMode.Negative)
            {
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]- 
                    var diagnosticMz = adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - 60.02167792 : theoreticalMz - 46.00602785;
                    //seek [M-C3H5]-
                    var threshold = 10.0;
                    var diagnosticMz2 = diagnosticMz - 12 * 3 - MassDiffDictionary.HydrogenMass * 5;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);
                    if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var SN1 = fattyacidProductIon(sn1Carbon, sn1Double);
                            var SN2 = fattyacidProductIon(sn2Carbon, sn2Double);

                            var query = new List<Peak> {
                                new Peak() { Mz = SN1, Intensity = 0.01 },
                                new Peak() { Mz = SN2, Intensity = 0.01 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 1)
                            { // 
                                var molecule = getPhospholipidMoleculeObjAsLevel2("DGTS", LbmClass.DGTS, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    return returnAnnotationResult("DGTS", LbmClass.DGTS, "", theoreticalMz, adduct,
                     totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }



        public static LipidMolecule JudgeIfLdgts(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {
                    // seek 144.10191 (C7H14NO2+)
                    var threshold = 1.0;
                    var diagnosticMz = 144.10191;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    //if (isClassIonFound == false) return null;
                    // seek 236.1492492 (C10H21NO5H +)
                    var threshold2 = 1.0;
                    var diagnosticMz2 = 236.1492492;
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon2Found == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    //var averageIntensity = 0.0;

                    //var molecule = getSingleacylchainMoleculeObjAsLevel2("LDGTS", LbmClass.LDGTS, totalCarbon, totalDoubleBond,
                    //averageIntensity);
                    //candidates.Add(molecule);

                    return returnAnnotationResult("LDGTS", LbmClass.LDGTS, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);

                }
            }
            else if (adduct.IonMode == IonMode.Negative)
            {
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]- 
                    var diagnosticMz = adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - 60.02167792 : theoreticalMz - 46.00602785;
                    //seek [M-C3H5]-
                    var threshold = 50.0;
                    var diagnosticMz2 = diagnosticMz - 12 * 3 - MassDiffDictionary.HydrogenMass * 5;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);
                    if (isClassIonFound == false) return null;
                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var SN1 = fattyacidProductIon(sn1Carbon, sn1Double);

                            var query = new List<Peak> {
                                new Peak() { Mz = SN1, Intensity = 0.01 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 1)
                            { // 
                                return returnAnnotationResult("LDGTS", LbmClass.LDGTS, "", theoreticalMz, adduct,
                                   totalCarbon, totalDoubleBond, 0, candidates, 1);
                            }
                        }
                    }
                }
            }

            return null;
        }

        public static LipidMolecule JudgeIfDgcc(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
           int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
           AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+") {
                    // seek 144.10191 (C7H14NO2+)
                    var threshold = 0.01;
                    var diagnosticMz = 104.106990495;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var diagnosticMz2 = 132.1019;
                    var isClassIonFound2 = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);

                    if (isClassIonFound == false || isClassIonFound2 == false) return null;

                    // check 184.07332 (C5H15NO4P)  
                    var threshold1 = 5.0;
                    var diagnosticMz1 = 184.07332;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    if (isClassIon1Found) return null; // reject PC

                    var threshold2 = 70.0;
                    var threshBegin = theoreticalMz - 90.0;
                    var threshEnd = theoreticalMz - 10.0;
                    var isPeakFound = isPeakFoundWithACritetion(spectrum, threshBegin, threshEnd, threshold2);// reject PC+Na
                    if (isPeakFound) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var nl_SN1 = theoreticalMz - acylCainMass(sn1Carbon, sn1Double) + MassDiffDictionary.HydrogenMass;
                            var nl_SN1_H2O = nl_SN1 - H2O;

                            var nl_SN2 = theoreticalMz - acylCainMass(sn2Carbon, sn2Double) + MassDiffDictionary.HydrogenMass;
                            var nl_NS2_H2O = nl_SN2 - H2O;

                            var query = new List<Peak> {
                                new Peak() { Mz = nl_SN1, Intensity = 0.01 },
                                new Peak() { Mz = nl_SN1_H2O, Intensity = 0.01 },
                                new Peak() { Mz = nl_SN2, Intensity = 0.01 },
                                new Peak() { Mz = nl_NS2_H2O, Intensity = 0.01 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2) { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("DGCC", LbmClass.DGCC, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    return returnAnnotationResult("DGCC", LbmClass.DGCC, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfLdgcc(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
           int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
           AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+") {
                    // seek 144.10191 (C7H14NO2+)
                    var threshold = 1.0;
                    var diagnosticMz = 104.106990495;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var diagnosticMz2 = 132.1019;
                    var isClassIonFound2 = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);

                    if (isClassIonFound == false || isClassIonFound2 == false) return null;
                    // check 184.07332 (C5H15NO4P)  
                    var threshold1 = 5.0;
                    var diagnosticMz1 = 184.07332;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    if (isClassIon1Found) return null; // reject PC

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    //var averageIntensity = 0.0;

                    //var molecule = getSingleacylchainMoleculeObjAsLevel2("LDGTS", LbmClass.LDGTS, totalCarbon, totalDoubleBond,
                    //averageIntensity);
                    //candidates.Add(molecule);

                    return returnAnnotationResult("LDGCC", LbmClass.LDGCC, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);

                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfGlcadg(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PS 46:6, totalCarbon = 46 and totalDoubleBond = 6
           int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
           AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    var threshold = 5.0;
                    var diagnosticMz = theoreticalMz - 17.026549 - 194.042652622; // seek [M-194.042652]- (C6H10O7 + H+)
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == !true ) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var nl_SN1 = diagnosticMz - acylCainMass(sn1Carbon, sn1Double) + MassDiffDictionary.HydrogenMass;
                            var nl_SN2 = diagnosticMz - acylCainMass(sn2Carbon, sn2Double) + MassDiffDictionary.HydrogenMass;

                            var query = new List<Peak> {
                                new Peak() { Mz = nl_SN1, Intensity = 10.0 },
                                new Peak() { Mz = nl_SN2, Intensity = 10.0 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { //
                                var molecule = getPhospholipidMoleculeObjAsLevel2("DGGA", LbmClass.DGGA, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (candidates.Count == 0 && isClassIonFound != true) return null;

                    //var score = 0;
                    //var molecule0 = getLipidAnnotaionAsLevel1("GlcADG", LbmClass.GlcADG, totalCarbon, totalDoubleBond, score, "");
                    //candidates.Add(molecule0);

                    return returnAnnotationResult("DGGA", LbmClass.DGGA, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 2);

                }
            }
            else
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = fattyacidProductIon(sn1Carbon, sn1Double);
                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);

                            var query = new List<Peak> {
                                new Peak() { Mz = sn1, Intensity = 5.0 },
                                new Peak() { Mz = sn2, Intensity = 5.0 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("DGGA", LbmClass.DGGA, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //var score = 0;
                    //var molecule0 = getLipidAnnotaionAsLevel1("GlcADG", LbmClass.GlcADG, totalCarbon, totalDoubleBond, score, "");
                    //candidates.Add(molecule0);
                    if (candidates.Count == 0) return null;

                    return returnAnnotationResult("DGGA", LbmClass.DGGA, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfAcylglcadg(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond,
           int minSn1Carbon, int maxSn1Carbon, int minSn1DoubleBond, int maxSn1DoubleBond,
           int minSn2Carbon, int maxSn2Carbon, int minSn2DoubleBond, int maxSn2DoubleBond,
           AdductIon adduct)
            {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSn1Carbon > totalCarbon) maxSn1Carbon = totalCarbon;
            if (maxSn1DoubleBond > totalDoubleBond) maxSn1DoubleBond = totalDoubleBond;
            if (maxSn2Carbon > totalCarbon) maxSn2Carbon = totalCarbon;
            if (maxSn2DoubleBond > totalDoubleBond) maxSn2DoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSn1Carbon; sn1Carbon <= maxSn1Carbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSn1DoubleBond; sn1Double <= maxSn1DoubleBond; sn1Double++)
                        {

                            var remainCarbon = totalCarbon - sn1Carbon;
                            var remainDouble = totalDoubleBond - sn1Double;
                            var carbonLimit = Math.Min(remainCarbon, maxSn2Carbon);
                            var doubleLimit = Math.Min(remainDouble, maxSn2DoubleBond);

                            for (int sn2Carbon = minSn2Carbon; sn2Carbon <= carbonLimit; sn2Carbon++)
                            {
                                for (int sn2Double = minSn2DoubleBond; sn2Double <= doubleLimit; sn2Double++)
                                {

                                    var sn3Carbon = totalCarbon - sn1Carbon - sn2Carbon;
                                    var sn3Double = totalDoubleBond - sn1Double - sn2Double;

                                    var sn1 = (MassDiffDictionary.CarbonMass * sn1Carbon)
                                            + (MassDiffDictionary.HydrogenMass * ((sn1Carbon * 2) - (sn1Double * 2))) + MassDiffDictionary.OxygenMass;
                                    var sn2 = (MassDiffDictionary.CarbonMass * sn2Carbon)
                                            + (MassDiffDictionary.HydrogenMass * ((sn2Carbon * 2) - (sn2Double * 2))) + MassDiffDictionary.OxygenMass;
                                    var sn3 = (MassDiffDictionary.CarbonMass * sn3Carbon)
                                            + (MassDiffDictionary.HydrogenMass * ((sn3Carbon * 2) - (sn3Double * 2))) + MassDiffDictionary.OxygenMass;

                                    var SN1Glc = sn1 + 194.042652622 - H2O - MassDiffDictionary.HydrogenMass; // 
                                    var SN2Gly = sn2 + 73.028416;//[SN2+C3H4O2+H]+
                                    var SN3Gly = sn3 + 73.028416;//[SN1+C3H4O2+H]+

                                    var query = new List<Peak> {
                                        new Peak() { Mz = SN1Glc, Intensity = 1 },
                                        new Peak() { Mz = SN2Gly, Intensity = 1 },
                                        new Peak() { Mz = SN3Gly, Intensity = 1 }
                                    };

                                    var foundCount = 0;
                                    var averageIntensity = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                    if (foundCount == 3)
                                    { // these three chains must be observed.
                                        var molecule = getAdggaMoleculeObjAsLevel2("ADGGA", LbmClass.ADGGA, sn1Carbon, sn1Double,
                                            sn2Carbon, sn2Double, sn3Carbon, sn3Double, averageIntensity);
                                        candidates.Add(molecule);
                                    }
                                }
                            }
                        }
                    }
                    //var score = 0;
                    //var molecule0 = getLipidAnnotaionAsLevel1("AcylGlcADG", LbmClass.AcylGlcADG, totalCarbon, totalDoubleBond, score, "");
                    //candidates.Add(molecule0);
                    if (candidates.Count == 0) return null;

                    return returnAnnotationResult("ADGGA", LbmClass.ADGGA, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 3);

                }
            }
            else
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSn1Carbon; sn1Carbon <= maxSn1Carbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSn1DoubleBond; sn1Double <= maxSn1DoubleBond; sn1Double++)
                        {
                            var remainCarbon = totalCarbon - sn1Carbon;
                            var remainDouble = totalDoubleBond - sn1Double;
                            var carbonLimit = Math.Min(remainCarbon, maxSn2Carbon);
                            var doubleLimit = Math.Min(remainDouble, maxSn2DoubleBond);

                            for (int sn2Carbon = minSn2Carbon; sn2Carbon <= carbonLimit; sn2Carbon++)
                            {
                                for (int sn2Double = minSn2DoubleBond; sn2Double <= doubleLimit; sn2Double++)
                                {

                                    var sn3Carbon = totalCarbon - sn1Carbon - sn2Carbon;
                                    var sn3Double = totalDoubleBond - sn1Double - sn2Double;

                                    var sn1 = fattyacidProductIon(sn1Carbon, sn1Double);
                                    var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);
                                    var sn3 = fattyacidProductIon(sn3Carbon, sn3Double);

                                    var query = new List<Peak> {
                                        new Peak() { Mz = sn1, Intensity = 1 },
                                        new Peak() { Mz = sn2, Intensity = 1 },
                                        new Peak() { Mz = sn3, Intensity = 1 }
                                    };

                                    var foundCount = 0;
                                    var averageIntensity = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                    if (foundCount == 3)
                                    { // these three chains must be observed.
                                        var molecule = getTriacylglycerolMoleculeObjAsLevel2("ADGGA", LbmClass.ADGGA, sn1Carbon, sn1Double,
                                            sn2Carbon, sn2Double, sn3Carbon, sn3Double, averageIntensity);
                                        candidates.Add(molecule);
                                    }
                                }
                            }
                        }
                    }
                    //var score = 0;
                    //var molecule0 = getLipidAnnotaionAsLevel1("AcylGlcADG", LbmClass.AcylGlcADG, totalCarbon, totalDoubleBond, score, "");
                    //candidates.Add(molecule0);
                    if (candidates.Count == 0) return null;

                    return returnAnnotationResult("ADGGA", LbmClass.ADGGA, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 3);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfSqdg(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PS 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+") // not found in lipidDbProject-Pos
                {
                    // seek -17.026549 (NH3)
                    var diagnosticMz = theoreticalMz - 17.026549;
                    // seek [M-C6H10O7S+H]+
                    var threshold = 1.0;
                    var diagnosticMz2 = diagnosticMz - (12 * 6 + MassDiffDictionary.HydrogenMass * 10 + MassDiffDictionary.OxygenMass * 7 + MassDiffDictionary.SulfurMass);
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);
                    if(isClassIonFound == !true) { return null; }

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var nl_SN1 = diagnosticMz2 - acylCainMass(sn1Carbon, sn1Double) - H2O + MassDiffDictionary.HydrogenMass;
                            var nl_SN2 = diagnosticMz2 - acylCainMass(sn2Carbon, sn2Double) - H2O + MassDiffDictionary.HydrogenMass;

                            var query = new List<Peak> {
                                new Peak() { Mz = nl_SN1, Intensity = 10.0 },
                                new Peak() { Mz = nl_SN2, Intensity = 10.0 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { //
                                var molecule = getPhospholipidMoleculeObjAsLevel2("SQDG", LbmClass.SQDG, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }

                    //var score = 0;
                    //var molecule0 = getLipidAnnotaionAsLevel1("SQDG", LbmClass.SQDG, totalCarbon, totalDoubleBond, score, "");
                    //candidates.Add(molecule0);

                    return returnAnnotationResult("SQDG", LbmClass.SQDG, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek 225.0069 (C6H9O7S-)
                    var threshold = 1.0;
                    var diagnosticMz = 225.0069;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;
                    
                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = fattyacidProductIon(sn1Carbon, sn1Double);
                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);
                            var nl_SN1 = theoreticalMz - acylCainMass(sn1Carbon, sn1Double) - H2O + Proton;
                            var nl_SN2 = theoreticalMz - acylCainMass(sn2Carbon, sn2Double) - H2O + Proton;

                            var query = new List<Peak> {
                                new Peak() { Mz = sn1, Intensity = 0.1 },
                                new Peak() { Mz = sn2, Intensity = 0.1 },
                                new Peak() { Mz = nl_SN1, Intensity = 0.1 },
                                new Peak() { Mz = nl_SN2, Intensity = 0.1 }
                            };
                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2)
                            { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("SQDG", LbmClass.SQDG, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //var score = 0;
                    //var molecule0 = getLipidAnnotaionAsLevel1("SQDG", LbmClass.SQDG, totalCarbon, totalDoubleBond, score, "");
                    //candidates.Add(molecule0);

                    return returnAnnotationResult("SQDG", LbmClass.SQDG, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfPetoh(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // seek -17.026549 (NH3)
                    var threshold = 1.0;
                    var diagnosticMz = theoreticalMz - 17.026549;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = acylCainMass(sn1Carbon, sn1Double) - Electron;
                            var sn2 = acylCainMass(sn2Carbon, sn2Double) - Electron;

                            var query = new List<Peak> {
                            new Peak() { Mz = sn1, Intensity = 0.1 },
                            new Peak() { Mz = sn2, Intensity = 0.1 }
                        };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("PEtOH", LbmClass.PEtOH, sn1Carbon, sn1Double, sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //var score = 0;
                    //var molecule0 = getLipidAnnotaionAsLevel1("PEtOH", LbmClass.PEtOH, totalCarbon, totalDoubleBond, score, "");
                    //candidates.Add(molecule0);

                    return returnAnnotationResult("PEtOH", LbmClass.PEtOH, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek 125.000919 (C2H6O4P-)
                    var threshold = 1;
                    var diagnosticMz = 125.000919;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = fattyacidProductIon(sn1Carbon, sn1Double);
                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);

                            var query = new List<Peak> {
                            new Peak() { Mz = sn1, Intensity = 5.0 },
                            new Peak() { Mz = sn2, Intensity = 5.0 }
                        };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { //
                                var molecule = getPhospholipidMoleculeObjAsLevel2("PEtOH", LbmClass.PEtOH, sn1Carbon, sn1Double, sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }

                    if (isClassIonFound == false && candidates.Count == 0) return null;

                    return returnAnnotationResult("PEtOH", LbmClass.PEtOH, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfPmeoh(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // seek -17.026549 (NH3)
                    var threshold = 1.0;
                    var diagnosticMz = theoreticalMz - 17.026549;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = acylCainMass(sn1Carbon, sn1Double) - Electron;
                            var sn2 = acylCainMass(sn2Carbon, sn2Double) - Electron;

                            var query = new List<Peak> {
                            new Peak() { Mz = sn1, Intensity = 0.1 },
                            new Peak() { Mz = sn2, Intensity = 0.1 }
                        };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("PMeOH", LbmClass.PMeOH, sn1Carbon, sn1Double, sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }

                    return returnAnnotationResult("PMeOH", LbmClass.PMeOH, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek 110.98527 (CH4O4P-)
                    var threshold = 1;
                    var diagnosticMz = 110.98527;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    var threshold2 = 30;
                    var diagnosticMz2 = theoreticalMz - 63.008491; // [M+C2H3N(ACN)+Na-2H]- adduct of EtherPE [M-H]- 
                    var isClassIonFound2 = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIonFound2) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = fattyacidProductIon(sn1Carbon, sn1Double);
                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);

                            var query = new List<Peak> {
                            new Peak() { Mz = sn1, Intensity = 5.0 },
                            new Peak() { Mz = sn2, Intensity = 5.0 }
                        };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { //
                                var molecule = getPhospholipidMoleculeObjAsLevel2("PMeOH", LbmClass.PMeOH, sn1Carbon, sn1Double, sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }

                    if (isClassIonFound == false && candidates.Count == 0) return null;

                    return returnAnnotationResult("PMeOH", LbmClass.PMeOH, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfPbtoh(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // seek 
                    // seek -17.026549 (NH3)
                    var threshold = 1.0;
                    var diagnosticMz = theoreticalMz - 17.026549;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = acylCainMass(sn1Carbon, sn1Double) - Electron;
                            var sn2 = acylCainMass(sn2Carbon, sn2Double) - Electron;

                            var query = new List<Peak> {
                            new Peak() { Mz = sn1, Intensity = 0.1 },
                            new Peak() { Mz = sn2, Intensity = 0.1 }
                        };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("PBtOH", LbmClass.PBtOH, sn1Carbon, sn1Double, sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }

                    return returnAnnotationResult("PBtOH", LbmClass.PBtOH, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek 153.03221938 (C4H10O4P-)
                    var threshold = 1;
                    var diagnosticMz = 153.03221938;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = fattyacidProductIon(sn1Carbon, sn1Double);
                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);

                            var query = new List<Peak> {
                            new Peak() { Mz = sn1, Intensity = 5.0 },
                            new Peak() { Mz = sn2, Intensity = 5.0 }
                        };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { //
                                var molecule = getPhospholipidMoleculeObjAsLevel2("PBtOH", LbmClass.PBtOH, sn1Carbon, sn1Double, sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }

                    if (isClassIonFound == false && candidates.Count == 0) return null;

                    return returnAnnotationResult("PBtOH", LbmClass.PMeOH, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }


        public static LipidMolecule JudgeIfHemiismonoacylglycerophosphate(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PS 46:6, totalCarbon = 46 and totalDoubleBond = 6
           int minSn1Carbon, int maxSn1Carbon, int minSn1DoubleBond, int maxSn1DoubleBond,
           int minSn2Carbon, int maxSn2Carbon, int minSn2DoubleBond, int maxSn2DoubleBond,
           AdductIon adduct){
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSn1Carbon > totalCarbon) maxSn1Carbon = totalCarbon;
            if (maxSn1DoubleBond > totalDoubleBond) maxSn1DoubleBond = totalDoubleBond;
            if (maxSn2Carbon > totalCarbon) maxSn2Carbon = totalCarbon;
            if (maxSn2DoubleBond > totalDoubleBond) maxSn2DoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSn1Carbon; sn1Carbon <= maxSn1Carbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSn1DoubleBond; sn1Double <= maxSn1DoubleBond; sn1Double++)
                        {
                            var remainCarbon = totalCarbon - sn1Carbon;
                            var remainDouble = totalDoubleBond - sn1Double;
                            var carbonLimit = Math.Min(remainCarbon, maxSn2Carbon);
                            var doubleLimit = Math.Min(remainDouble, maxSn2DoubleBond);

                            for (int sn2Carbon = minSn2Carbon; sn2Carbon <= carbonLimit; sn2Carbon++)
                            {
                                for (int sn2Double = minSn2DoubleBond; sn2Double <= doubleLimit; sn2Double++)
                                {

                                    var sn3Carbon = totalCarbon - sn1Carbon - sn2Carbon;
                                    var sn3Double = totalDoubleBond - sn1Double - sn2Double;

                                    var sn1 = (MassDiffDictionary.CarbonMass * sn1Carbon)
                                            + (MassDiffDictionary.HydrogenMass * ((sn1Carbon * 2) - (sn1Double * 2))) + MassDiffDictionary.OxygenMass;
                                    var sn2 = (MassDiffDictionary.CarbonMass * sn2Carbon)
                                            + (MassDiffDictionary.HydrogenMass * ((sn2Carbon * 2) - (sn2Double * 2))) + MassDiffDictionary.OxygenMass;
                                    var sn3 = (MassDiffDictionary.CarbonMass * sn3Carbon)
                                            + (MassDiffDictionary.HydrogenMass * ((sn3Carbon * 2) - (sn3Double * 2))) + MassDiffDictionary.OxygenMass;

                                    var SN1Gly = sn1 + 73.028416;//[SN1+C3H4O2+H]+
                                    var SN2Gly = sn2 + 73.028416;//[SN2+C3H4O2+H]+
                                    var SN3Gly = sn3 + 73.028416;//[SN3+C3H4O2+H]+

                                    var query = new List<Peak> {
                                        new Peak() { Mz = SN1Gly, Intensity = 1.0 },
                                        new Peak() { Mz = SN2Gly, Intensity = 1.0 },
                                        new Peak() { Mz = SN3Gly, Intensity = 1.0 },
                                    };

                                    var foundCount = 0;
                                    var averageIntensity = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                    if (foundCount == 3)
                                    {
                                        var NL_SN3PA = theoreticalMz - 17.026549 - SN1Gly - 97.976897 + MassDiffDictionary.HydrogenMass; // [M-(sn3+C3H4O2+H)-H3PO4+H]+
                                        var threshold = 5;
                                        var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, NL_SN3PA, threshold);
                                        if (isClassIonFound == true)
                                        {
                                            var molecule = getAcylglycerolMoleculeObjAsLevel2("HBMP", LbmClass.HBMP, sn1Carbon, sn1Double,
                                                 sn2Carbon, sn2Double, sn3Carbon, sn3Double, averageIntensity);
                                            candidates.Add(molecule);
                                        }
                                        else
                                        {
                                            var molecule = getTriacylglycerolMoleculeObjAsLevel2("HBMP", LbmClass.HBMP, sn1Carbon, sn1Double,
                                                sn2Carbon, sn2Double, sn3Carbon, sn3Double, averageIntensity);
                                            candidates.Add(molecule);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    //var score = 0;
                    //var molecule0 = getLipidAnnotaionAsLevel1("HBMP", LbmClass.HBMP, totalCarbon, totalDoubleBond, score, "");
                    //candidates.Add(molecule0);
                    if (candidates.Count == 0) return null;
                    return returnAnnotationResult("HBMP", LbmClass.HBMP, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 3);
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSn1Carbon; sn1Carbon <= maxSn1Carbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSn1DoubleBond; sn1Double <= maxSn1DoubleBond; sn1Double++)
                        {
                            var remainCarbon = totalCarbon - sn1Carbon;
                            var remainDouble = totalDoubleBond - sn1Double;
                            var carbonLimit = Math.Min(remainCarbon, maxSn2Carbon);
                            var doubleLimit = Math.Min(remainDouble, maxSn2DoubleBond);

                            for (int sn2Carbon = minSn2Carbon; sn2Carbon <= carbonLimit; sn2Carbon++)
                            {
                                for (int sn2Double = minSn2DoubleBond; sn2Double <= doubleLimit; sn2Double++)
                                {
                                    var sn3Carbon = totalCarbon - sn1Carbon - sn2Carbon;
                                    //if (sn3Carbon > maxSn1Carbon) break;

                                    var sn3Double = totalDoubleBond - sn1Double - sn2Double;

                                    var sn1 = fattyacidProductIon(sn1Carbon, sn1Double);
                                    var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);
                                    var sn3 = fattyacidProductIon(sn3Carbon, sn3Double);

                                    var query = new List<Peak> {
                                        new Peak() { Mz = sn1, Intensity = 0.1 },
                                        new Peak() { Mz = sn2, Intensity = 0.1 },
                                        new Peak() { Mz = sn3, Intensity = 0.1 }
                                    };
                                    var foundCount = 0;
                                    var averageIntensity = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                    if (foundCount == 3)
                                    {
                                        var SN1PA = sn1 + 135.993094251; // [FA1+C3H6O4P-H]-
                                        var NL_sn1 = theoreticalMz - sn1 - MassDiffDictionary.HydrogenMass; // which is better ?
                                        var threshold = 0.1;
                                        var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, SN1PA, threshold);
                                        var isClassIonFound2 = isDiagnosticFragmentExist(spectrum, ms2Tolerance, NL_sn1, threshold);

                                        if (isClassIonFound == true || isClassIonFound2 == true)
                                        {
                                            var molecule = getAcylglycerolMoleculeObjAsLevel2("HBMP", LbmClass.HBMP, sn1Carbon, sn1Double,
                                                 sn2Carbon, sn2Double, sn3Carbon, sn3Double, averageIntensity);
                                            candidates.Add(molecule);
                                        }
                                        else
                                        {
                                            var molecule = getTriacylglycerolMoleculeObjAsLevel2("HBMP", LbmClass.HBMP, sn1Carbon, sn1Double,
                                                sn2Carbon, sn2Double, sn3Carbon, sn3Double, averageIntensity);
                                            candidates.Add(molecule);
                                        }
                                     }
                                }
                            }
                        }
                    }
                    if (candidates.Count == 0) return null;

                    //var score = 0;
                    //var molecule0 = getLipidAnnotaionAsLevel1("HBMP", LbmClass.HBMP, totalCarbon, totalDoubleBond, score, "");
                    //candidates.Add(molecule0);

                    return returnAnnotationResult("HBMP", LbmClass.HBMP, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 3);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfCardiolipin(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond,
           int minSn1Carbon, int maxSn1Carbon, int minSn1DoubleBond, int maxSn1DoubleBond,
           int minSn2Carbon, int maxSn2Carbon, int minSn2DoubleBond, int maxSn2DoubleBond,
           int minSn3Carbon, int maxSn3Carbon, int minSn3DoubleBond, int maxSn3DoubleBond,
           AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;

            var maxSnCarbon1_2 = maxSn1Carbon + maxSn2Carbon;
            var maxSnDoubleBond1_2 = maxSn1DoubleBond + maxSn2DoubleBond;
            if (maxSnCarbon1_2 > totalCarbon) maxSnCarbon1_2 = totalCarbon;
            if (maxSnDoubleBond1_2 > totalDoubleBond) maxSnDoubleBond1_2 = totalDoubleBond;

            var minSnCarbon1_2 = minSn1Carbon + minSn2Carbon;
            var minSnDoubleBond1_2 = minSn1DoubleBond + minSn2DoubleBond;


            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // seek -17.026549 (NH3)
                    var threshold = 1.0;
                    var diagnosticMz = theoreticalMz - 17.026549;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();

                    // maybe fatty acid product ion is not found in positive mode

                    for (int sn1_2Carbon = minSnCarbon1_2; sn1_2Carbon <= Math.Floor((double)totalCarbon / 2); sn1_2Carbon++)
                    {
                        for (int sn1_2Double = minSnDoubleBond1_2; sn1_2Double <= maxSnDoubleBond1_2; sn1_2Double++)
                        {

                            var sn3_4Carbon = totalCarbon - sn1_2Carbon;
                            var sn3_4Double = totalDoubleBond - sn1_2Double;

                            var SN1_2 = acylCainMass(sn1_2Carbon, sn1_2Double);
                            var SN3_4 = acylCainMass(sn3_4Carbon, sn3_4Double);

                            var SN1SN2Gly = SN1_2 + (12 * 3 + MassDiffDictionary.HydrogenMass * 3) + MassDiffDictionary.OxygenMass * 3 + Proton; //2*acyl + glycerol
                            var SN3SN4Gly = SN3_4 + (12 * 3 + MassDiffDictionary.HydrogenMass * 3) + MassDiffDictionary.OxygenMass * 3 + Proton; //

                            var query = new List<Peak>
                            {
                                new Peak() { Mz = SN1SN2Gly, Intensity = 50 },
                                new Peak() { Mz = SN3SN4Gly, Intensity = 50 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            {
                                var molecule = getCardiolipinMoleculeObjAsLevel2_0("CL", LbmClass.CL, sn1_2Carbon, sn1_2Double,
                                sn3_4Carbon, sn3_4Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //var score = 0;
                    //var molecule0 = getLipidAnnotaionAsLevel1("CL", LbmClass.CL, totalCarbon, totalDoubleBond, score, "");
                    //candidates.Add(molecule0);

                    return returnAnnotationResult("CL", LbmClass.CL, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 4);
                }
            }
                else if (adduct.AdductIonName == "[M-H]-")
            {
                // seek 152.995836 (C3H6O5P-)
                var threshold = 1.0;
                var diagnosticMz = 152.995836;
                var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                //if (isClassIonFound == false) return null;

                // from here, acyl level annotation is executed.
                var candidates = new List<LipidMolecule>();
                for (int sn1_2Carbon = minSnCarbon1_2; sn1_2Carbon <= Math.Floor((double)totalCarbon / 2); sn1_2Carbon++)
                {
                    for (int sn1_2Double = minSnDoubleBond1_2; sn1_2Double <= maxSnDoubleBond1_2; sn1_2Double++)
                    {

                        var sn3_4Carbon = totalCarbon - sn1_2Carbon;
                        var sn3_4Double = totalDoubleBond - sn1_2Double;

                        var SN1_SN2 = acylCainMass(sn1_2Carbon, sn1_2Double) + 12 * 3 + (MassDiffDictionary.OxygenMass * 7) + (MassDiffDictionary.HydrogenMass * 6) + MassDiffDictionary.PhosphorusMass - Proton ; //[SN1+SN2+C3H6O7P]-
                        var SN3_SN4 = acylCainMass(sn3_4Carbon, sn3_4Double) + 12 * 3 + (MassDiffDictionary.OxygenMass * 7) + (MassDiffDictionary.HydrogenMass * 6) + MassDiffDictionary.PhosphorusMass - Proton ; //[SN3+SN4+C3H6O7P]-

                        //Console.WriteLine(sn1_2Carbon + ":" + sn1_2Double + "-" + sn3_4Carbon + ":" + sn3_4Double + " " + SN1_SN2 + " " + SN3_SN4);

                        var query2 = new List<Peak>
                                            {
                                                new Peak() { Mz = SN1_SN2, Intensity = 1.0 },
                                                new Peak() { Mz = SN3_SN4, Intensity = 1.0 }
                                            };
                        var foundCount2 = 0;
                        var averageIntensity2 = 0.0;
                        countFragmentExistence(spectrum, query2, ms2Tolerance, out foundCount2, out averageIntensity2);

                        var carbonLimit = Math.Min(sn3_4Carbon, maxSn3Carbon);
                        var doubleLimit = Math.Min(sn3_4Double, maxSn3DoubleBond);

                        if (foundCount2 >= 1)
                        {
                            for (int sn3Carbon = minSn3Carbon; sn3Carbon <= carbonLimit; sn3Carbon++)
                            {
                                for (int sn3Double = minSn3DoubleBond; sn3Double <= doubleLimit; sn3Double++)
                                {

                                    var sn4Carbon = sn3_4Carbon - sn3Carbon;
                                    var sn4Double = sn3_4Double - sn3Double;
                                    var carbonLimit2 = Math.Min(sn1_2Carbon, maxSn1Carbon);
                                    var doubleLimit2 = Math.Min(sn1_2Double, maxSn1DoubleBond);

                                    for (int sn1Carbon = minSn1Carbon; sn1Carbon <= carbonLimit2; sn1Carbon++)
                                    {
                                        for (int sn1Double = minSn1DoubleBond; sn1Double <= doubleLimit2; sn1Double++)
                                        {
                                            if (sn1Double > 0)
                                            {
                                                if ((double)(sn1Carbon / sn1Double) < 3) break;
                                            }

                                            var sn2Carbon = sn1_2Carbon - sn1Carbon;

                                            //if (sn3_4Carbon + sn1Carbon + sn2Carbon != totalCarbon) { break; }
                                            if (sn2Carbon < minSn2Carbon) { break; }

                                            var sn2Double = sn1_2Double - sn1Double;
                                            if (sn2Double > 0)
                                            {
                                                if ((double)(sn2Carbon / sn2Double) < 3) break;
                                            }

                                            var SN1 = fattyacidProductIon(sn1Carbon, sn1Double);
                                            var SN2 = fattyacidProductIon(sn2Carbon, sn2Double);
                                            var SN3 = fattyacidProductIon(sn3Carbon, sn3Double);
                                            var SN4 = fattyacidProductIon(sn4Carbon, sn4Double);

                                            //Console.WriteLine(sn1Carbon + ":" + sn1Double + "-" + sn2Carbon + ":" + sn2Double + "-" +
                                            //    sn3Carbon + ":" + sn3Double + "-" + sn4Carbon + ":" + sn4Double + " " +
                                            //    SN1 + " " + SN2 + " " + SN3 + " " + SN4);



                                            var query = new List<Peak>
                                                {
                                                    new Peak() { Mz = SN1, Intensity = 1 },
                                                    new Peak() { Mz = SN2, Intensity = 1 },
                                                    new Peak() { Mz = SN3, Intensity = 1 },
                                                    new Peak() { Mz = SN4, Intensity = 1 },
                                                };

                                            var foundCount = 0;
                                            var averageIntensity = 0.0;
                                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                            if (foundCount >= 3)
                                            {
                                                averageIntensity = averageIntensity + averageIntensity2;
                                                if (averageIntensity > 100) averageIntensity = 100;
                                                 var molecule = getCardiolipinMoleculeObjAsLevel2_2("CL", LbmClass.CL, sn1Carbon, sn2Carbon,
                                                                sn3Carbon, sn4Carbon, sn1Double, sn2Double, sn3Double, sn4Double, averageIntensity);
                                                candidates.Add(molecule);
                                            }
                                       }
                                    }
                                }
                            }
                            if (candidates.Count == 0)
                            {
                                var molecule1 = getCardiolipinMoleculeObjAsLevel2_0("CL", LbmClass.CL, sn1_2Carbon, sn1_2Double,
                                    sn3_4Carbon, sn3_4Double, averageIntensity2);
                                candidates.Add(molecule1);
                            }
                        }
                    }
                }

                //var score = 0;
                //var molecule0 = getLipidAnnotaionAsLevel1("CL", LbmClass.CL, totalCarbon, totalDoubleBond, score, "");
                //candidates.Add(molecule0);
                if (candidates == null || candidates.Count == 0) return null;
                return returnAnnotationResult("CL", LbmClass.CL, "", theoreticalMz, adduct,
                   totalCarbon, totalDoubleBond, 0, candidates, 4);

            }

            return null;
        }

        public static LipidMolecule JudgeIfCardiolipin(ObservableCollection<double[]> spectrum, double ms2Tolerance,
                   double theoreticalMz, int totalCarbon, int totalDoubleBond,
                   int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
                   AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;

            //var max2SnCarbon = maxSnCarbon * 2;
            //var max2SnDoubleBond = maxSnDoubleBond * 2;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;

            //if (max2SnCarbon > totalCarbon) max2SnCarbon = totalCarbon;
            //if (max2SnDoubleBond > totalDoubleBond) max2SnDoubleBond = totalDoubleBond;


            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+") {
                    // seek -17.026549 (NH3)
                    var threshold = 1.0;
                    var diagnosticMz = theoreticalMz - 17.026549;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();

                    // maybe fatty acid product ion is not found in positive mode

                    for (int sn1_2Carbon = minSnCarbon; sn1_2Carbon <= maxSnCarbon; sn1_2Carbon++) {
                        for (int sn1_2Double = minSnDoubleBond; sn1_2Double <= maxSnDoubleBond; sn1_2Double++) {
                            //var remainCarbon = totalCarbon - sn1_2Carbon;
                            //var remainDouble = totalDoubleBond - sn1_2Double;
                            //var carbonLimit = Math.Min(remainCarbon, maxSnCarbon);
                            //var doubleLimit = Math.Min(remainDouble, maxSnDoubleBond);

                            //var sn3_4Carbon = Math.Min(totalCarbon - sn1_2Carbon, maxSnCarbon);
                            var sn3_4Carbon = totalCarbon - sn1_2Carbon;
                            var sn3_4Double = totalDoubleBond - sn1_2Double;

                            var SN1_2 = acylCainMass(sn1_2Carbon, sn1_2Double);
                            var SN3_4 = acylCainMass(sn3_4Carbon, sn3_4Double);

                            var SN1SN2Gly = SN1_2 + (12 * 3 + MassDiffDictionary.HydrogenMass * 3) + MassDiffDictionary.OxygenMass * 3 + Proton; //2*acyl + glycerol
                            var SN3SN4Gly = SN3_4 + (12 * 3 + MassDiffDictionary.HydrogenMass * 3) + MassDiffDictionary.OxygenMass * 3 + Proton; //

                            var query = new List<Peak>
                            {
                                new Peak() { Mz = SN1SN2Gly, Intensity = 1 },
                                new Peak() { Mz = SN3SN4Gly, Intensity = 1 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2) {
                                var molecule = getCardiolipinMoleculeObjAsLevel2_0("CL", LbmClass.CL, sn1_2Carbon, sn1_2Double,
                                sn3_4Carbon, sn3_4Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //var score = 0;
                    //var molecule0 = getLipidAnnotaionAsLevel1("CL", LbmClass.CL, totalCarbon, totalDoubleBond, score, "");
                    //candidates.Add(molecule0);

                    return returnAnnotationResult("CL", LbmClass.CL, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 4);
                }
            }

            return null;
        }



        public static LipidMolecule JudgeIfLysocardiolipin(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond,
           int minSn1Carbon, int maxSn1Carbon, int minSn1DoubleBond, int maxSn1DoubleBond,
           int minSn2Carbon, int maxSn2Carbon, int minSn2DoubleBond, int maxSn2DoubleBond,
           AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSn1Carbon > totalCarbon) maxSn1Carbon = totalCarbon;
            if (maxSn1DoubleBond > totalDoubleBond) maxSn1DoubleBond = totalDoubleBond;
            if (maxSn2Carbon > totalCarbon) maxSn2Carbon = totalCarbon;
            if (maxSn2DoubleBond > totalDoubleBond) maxSn2DoubleBond = totalDoubleBond;

            if (adduct.IonMode == IonMode.Negative)
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek 152.995836 (C3H6O5P-)
                    var threshold = 1.0;
                    var diagnosticMz = 152.995836;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;
                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSn1Carbon; sn1Carbon <= maxSn1Carbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSn1DoubleBond; sn1Double <= maxSn1DoubleBond; sn1Double++)
                        {

                            var remainCarbon = totalCarbon - sn1Carbon;
                            var remainDouble = totalDoubleBond - sn1Double;
                            var carbonLimit = Math.Min(remainCarbon, maxSn2Carbon);
                            var doubleLimit = Math.Min(remainDouble, maxSn2DoubleBond);

                            for (int sn2Carbon = minSn2Carbon; sn2Carbon <= carbonLimit; sn2Carbon++)
                            {
                                for (int sn2Double = minSn2DoubleBond; sn2Double <= doubleLimit; sn2Double++)
                                {
                                    var sn3Carbon = totalCarbon - sn1Carbon - sn2Carbon;
                                    //if (sn1Carbon + sn2Carbon + sn3Carbon != totalCarbon) { break; }
                                    var sn3Double = remainDouble - sn2Double;
                                    if (sn3Double < 0) { break; }

                                    var SN1 = fattyacidProductIon(sn1Carbon, sn1Double);
                                    var SN2 = fattyacidProductIon(sn2Carbon, sn2Double);
                                    var SN3 = fattyacidProductIon(sn3Carbon, sn3Double);

                                    var SN1_PA = acylCainMass(sn1Carbon, sn1Double) + 12 * 3 + (MassDiffDictionary.OxygenMass * 6)
                                            + (MassDiffDictionary.HydrogenMass * 6) + MassDiffDictionary.PhosphorusMass - Proton; //[SN1+C3H7O4P]-
                                    var SN2_SN3_PA = acylCainMass(sn2Carbon+ sn3Carbon, sn2Double+ sn3Double) + 12 * 3 + (MassDiffDictionary.OxygenMass * 7) 
                                            + (MassDiffDictionary.HydrogenMass * 8) + MassDiffDictionary.PhosphorusMass - Proton; //[SN2+SN3+C3H8O7P]-


                                    var query = new List<Peak>
                                        {
                                        new Peak() { Mz = SN1, Intensity = 0.1 },
                                        new Peak() { Mz = SN2, Intensity = 5 },
                                        new Peak() { Mz = SN3, Intensity = 5 },
                                        };

                                    var query2 = new List<Peak>
                                        {
                                        new Peak() { Mz = SN1_PA, Intensity = 5 },
                                        new Peak() { Mz = SN2_SN3_PA, Intensity = 5 }
                                        };

                                    var foundCount = 0;
                                    var foundCount2 = 0;

                                    var averageIntensity = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);
                                    var averageIntensity2 = 0.0;
                                    countFragmentExistence(spectrum, query2, ms2Tolerance, out foundCount2, out averageIntensity2);

                                    if (foundCount == 3 && foundCount2 == 2)
                                    {
                                        var molecule = getLysocardiolipinMoleculeObjAsLevel2("MLCL", LbmClass.MLCL, sn1Carbon, sn2Carbon,
                                                    sn3Carbon, sn1Double, sn2Double, sn3Double, averageIntensity);
                                        candidates.Add(molecule);
                                    }
                                    else if (foundCount < 3 && foundCount >= 0 && foundCount2 == 2)
                                    {
                                    var molecule = getCardiolipinMoleculeObjAsLevel2_0("MLCL", LbmClass.MLCL, sn1Carbon, sn1Double,
                                        sn2Carbon + sn3Carbon, sn2Double + sn3Double, averageIntensity);
                                            candidates.Add(molecule);
                                    }
                                    else if (foundCount == 3 && foundCount2 < 2)
                                    {
                                        var molecule = getTriacylglycerolMoleculeObjAsLevel2("MLCL", LbmClass.MLCL, sn1Carbon, sn1Double,
                                                        sn2Carbon, sn2Double, sn3Carbon, sn3Double, averageIntensity);
                                        candidates.Add(molecule);
                                    }
                                }

                            }
                        }
                    }
                    if (candidates.Count == 0) return null;

                    return returnAnnotationResult("MLCL", LbmClass.MLCL, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 3);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfDilysocardiolipin(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond,
           int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
           AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;

            if (adduct.IonMode == IonMode.Negative)
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek 152.995836 (C3H6O5P-)
                    var threshold = 1.0;
                    var diagnosticMz = 152.995836;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            var sn2Carbon = totalCarbon - sn1Carbon;
                           // if (sn2Carbon < minSnCarbon) { break; }

                            var sn2Double = totalDoubleBond - sn1Double;
                            //if (sn2Double < 0 ) { break; }


                            var SN1_PA = acylCainMass(sn1Carbon, sn1Double) + 12 * 3 + (MassDiffDictionary.OxygenMass * 6)
                                    + (MassDiffDictionary.HydrogenMass * 8) + MassDiffDictionary.PhosphorusMass - Proton; //[SN1+C3H8O4P]-
                            var SN2_PA = acylCainMass(sn2Carbon, sn2Double) + 12 * 3 + (MassDiffDictionary.OxygenMass * 6)
                                    + (MassDiffDictionary.HydrogenMass * 8) + MassDiffDictionary.PhosphorusMass - Proton; //[SN1+C3H8O4P]-


                            var query = new List<Peak>
                                        {
                                        new Peak() { Mz = SN1_PA, Intensity = 5.0 },
                                        new Peak() { Mz = SN2_PA, Intensity = 5.0 },
                                        };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            {
                                var molecule = getCardiolipinMoleculeObjAsLevel2_0("DLCL", LbmClass.DLCL, sn1Carbon, sn1Double,
                                sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //var score = 0;
                    //var molecule0 = getLipidAnnotaionAsLevel1("DLCL", LbmClass.DLCL, totalCarbon, totalDoubleBond, score, "");
                    //candidates.Add(molecule0);

                    return returnAnnotationResult("DLCL", LbmClass.DLCL, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfFattyacid(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond,
           int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
           AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;

            if (adduct.IonMode == IonMode.Negative)
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    var threshold = 5.0;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, theoreticalMz, threshold);
                    if (isClassIonFound == false) return null;
                    var candidates = new List<LipidMolecule>();
                    //var averageIntensity = 0.0;
                    //var molecule0 = getSingleacylchainMoleculeObjAsLevel2("FA", LbmClass.FA, totalCarbon, totalDoubleBond, averageIntensity);
                    //candidates.Add(molecule0);

                    return returnAnnotationResult("FA", LbmClass.FA, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfOxfattyacid(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond,
             int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
                AdductIon adduct, int totalOxidized)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;

            if (adduct.IonMode == IonMode.Negative)
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    var threshold = 1.0;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, theoreticalMz, threshold);
                    if (isClassIonFound == false) return null;
                    var candidates = new List<LipidMolecule>();
                    //var averageIntensity = 0.0;

                    //var molecule = getSingleacyloxMoleculeObjAsLevel1("OxFA", LbmClass.OxFA, totalCarbon, totalDoubleBond, totalOxidized, averageIntensity);
                    //candidates.Add(molecule);

                    return returnAnnotationResult("FA", LbmClass.OxFA, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, totalOxidized, candidates, 1);
                }
            }
            return null;
        }



        public static LipidMolecule JudgeIfFahfa(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond,
           int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
           AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;

            if (adduct.IonMode == IonMode.Negative)
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            if (sn1Double > 0)
                            {
                                if ((double)(sn1Carbon / sn1Double) < 3) break;
                            }

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            if (sn2Double > 0)
                            {
                                if ((double)(sn2Carbon / sn2Double) < 3) break;
                            }

                            var NL_SN1 = theoreticalMz - acylCainMass(sn1Carbon, sn1Double) + MassDiffDictionary.HydrogenMass; //[M-SN1(HFA)-H]-
                            var NL_SN2 = theoreticalMz - acylCainMass(sn2Carbon, sn2Double) - MassDiffDictionary.OxygenMass + MassDiffDictionary.HydrogenMass; //[M - SN2-H]-

                            var query = new List<Peak>
                                        {
                                        new Peak() { Mz = NL_SN1, Intensity = 10.0 },
                                        new Peak() { Mz = NL_SN2, Intensity = 1.0 },
                                        };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2)
                            {
                                var molecule = getFahfaMoleculeObjAsLevel2_0("FAHFA", LbmClass.FAHFA, sn1Carbon, sn1Double,
                                sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (candidates.Count == 0) return null;

                    //var score = 0;
                    //var molecule0 = getLipidAnnotaionAsLevel1("FAHFA", LbmClass.FAHFA, totalCarbon, totalDoubleBond, score, "");
                    //candidates.Add(molecule0);

                    return returnAnnotationResult("FAHFA", LbmClass.FAHFA, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

///////////// ceramide section
        public static LipidMolecule JudgeIfCeramidens(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {
                    // seek -H2O
                    var threshold = 5.0;
                    var diagnosticMz = theoreticalMz - H2O;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) // sphingo chain must have double bond
                        {

                            var acylCarbon = totalCarbon - sphCarbon;
                           // if (acylCarbon < minSphCarbon) { break; }
                            var acylDouble = totalDoubleBond - sphDouble;
                            if (acylDouble >= 7) continue;
                            var sph1 = diagnosticMz - acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.HydrogenMass;
                            var sph2 = sph1 - H2O;
                            var sph3 = sph2 - 12; //[Sph-CH4O2+H]+
                            var acylamide = acylCarbon * 12 + (((2 * acylCarbon) - (2 * acylDouble) + 2) * MassDiffDictionary.HydrogenMass) + MassDiffDictionary.OxygenMass + MassDiffDictionary.NitrogenMass;

                            // must query
                            var queryMust = new List<Peak> {
                                new Peak() { Mz = sph2, Intensity = 5 },
                            };
                            var foundCountMust = 0;
                            var averageIntensityMust = 0.0;
                            countFragmentExistence(spectrum, queryMust, ms2Tolerance, out foundCountMust, out averageIntensityMust);
                            if (foundCountMust == 0) continue;

                            var query = new List<Peak> {
                                new Peak() { Mz = sph1, Intensity = 0.01 },
                                new Peak() { Mz = sph3, Intensity = 0.01 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);
                            var foundCountThresh = acylCarbon < 12 ? 2 : 1; // to exclude strange annotation

                            if (foundCount >= foundCountThresh)
                            { // 
                                var molecule = getCeramideMoleculeObjAsLevel2("Cer", LbmClass.Cer_NS, "d", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (candidates.Count == 0) return null;

                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("Cer-NS", LbmClass.Cer_NS, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}

                    return returnAnnotationResult("Cer", LbmClass.Cer_NS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);

                }
            }
            else
            {
                if (adduct.AdductIonName == "[M-H]-" || adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    //var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M-H]-" ? theoreticalMz :
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                    // seek [M-CH2O-H]-
                    var threshold1 = 1.0;
                    var diagnosticMz1 = diagnosticMz - 12 - H2O ;
                    // seek [M-CH2O-H2O-H]-
                    var threshold2 = 1.0;
                    var diagnosticMz2 = diagnosticMz1 - H2O;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon1Found != true && isClassIon2Found != true) return null;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz3 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold3 = 50.0;
                        var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                        if (isClassIon3Found) return null;
                    }

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++)
                        {

                            var acylCarbon = totalCarbon - sphCarbon;
                            //if (acylCarbon < minSphCarbon) { break; }
                            var acylDouble = totalDoubleBond - sphDouble;
                            if (acylDouble >= 7) continue;

                            var sphChain_loss = diagnosticMz - ((sphCarbon - 2) * 12) - (MassDiffDictionary.HydrogenMass * ((sphCarbon - 2) * 2 - sphDouble * 2 + 1)) -
                                     2 * MassDiffDictionary.OxygenMass - MassDiffDictionary.HydrogenMass; // as [FA+NCCO-3H]- on Excel(may be not sure)
                            var sphFragment = ((sphCarbon - 2) * 12) + (MassDiffDictionary.HydrogenMass * ((sphCarbon - 2) * 2 - sphDouble * 2) - 1) + MassDiffDictionary.OxygenMass; // [Sph-NCC-3H]-
                            var acylFragment = fattyacidProductIon(acylCarbon, acylDouble) - MassDiffDictionary.OxygenMass - 2 * MassDiffDictionary.HydrogenMass; // 

                            //Console.WriteLine("d" + sphCarbon + ":" + sphDouble + "/" + acylCarbon + ":" + acylDouble + " " +
                            //    sphChain_loss + " " + sphFragment + " " + acylFragment);
                            var query = new List<Peak> {
                                new Peak() { Mz = sphChain_loss, Intensity = 5 },
                                new Peak() { Mz = sphFragment, Intensity = 1 },
                                new Peak() { Mz = acylFragment, Intensity = 1 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2)
                            { // the diagnostic acyl ion must be observed for level 2 annotation
                                var molecule = getCeramideMoleculeObjAsLevel2("Cer", LbmClass.Cer_NS, "d", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("Cer-NS", LbmClass.Cer_NS, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}
                    if (candidates.Count == 0) return null;
                    return returnAnnotationResult("Cer", LbmClass.Cer_NS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfCeramidePhosphate(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
            AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+") {
                    var diagnosticMz = theoreticalMz - 79.966333 - H2O;
                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++) {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) // sphingo chain must have double bond
                        {
                            var acylCarbon = totalCarbon - sphCarbon;
                            var acylDouble = totalDoubleBond - sphDouble;

                            var sph1 = diagnosticMz - acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.HydrogenMass;
                            var sph2 = sph1 - H2O;
                            var sph3 = sph2 - 12; //[Sph-CH4O2+H]+
                            var acylamide = acylCarbon * 12 + (((2 * acylCarbon) - (2 * acylDouble) + 2) * MassDiffDictionary.HydrogenMass) + MassDiffDictionary.OxygenMass + MassDiffDictionary.NitrogenMass;

                            // must query
                            var queryMust = new List<Peak> {
                                new Peak() { Mz = sph2, Intensity = 10 },
                            };
                            var foundCountMust = 0;
                            var averageIntensityMust = 0.0;
                            countFragmentExistence(spectrum, queryMust, ms2Tolerance, out foundCountMust, out averageIntensityMust);
                            if (foundCountMust == 1) { 
                                var molecule = getCeramideMoleculeObjAsLevel2("CerP", LbmClass.CerP, "d", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, averageIntensityMust);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (candidates.Count == 0) return null;
                    return returnAnnotationResult("CerP", LbmClass.CerP, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else {
                if (adduct.AdductIonName == "[M-H]-") {
                    var diagnosticMz = theoreticalMz;
                    // seek [M-CH2O-H]-
                    var threshold1 = 1.0;
                    var diagnosticMz1 = 96.969619122;
                    // seek [M-CH2O-H2O-H]-
                    var threshold2 = 1.0;
                    var diagnosticMz2 = 78.959054438;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon1Found && isClassIon2Found) {
                        return returnAnnotationResult("CerP", LbmClass.CerP, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, new List<LipidMolecule>(), 2);
                    }
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfCeramidends(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {
                    // seek -H2O
                    var threshold = 5.0;
                    var diagnosticMz = theoreticalMz - H2O;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        int sphDouble = 0; // Cer-NDS sphingo chain don't have double bond

                        var acylCarbon = totalCarbon - sphCarbon;
                            //if (acylCarbon < minSphCarbon) { break; }
                        var acylDouble = totalDoubleBond - sphDouble;

                        var sph1 = diagnosticMz - acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.HydrogenMass;
                        var sph2 = sph1 - H2O;
                        var sph3 = sph2 - 12; //[Sph-CH4O2+H]+
                        var acylamide = acylCarbon * 12 + (((2 * acylCarbon) - (2 * acylDouble) + 2) * MassDiffDictionary.HydrogenMass) + MassDiffDictionary.OxygenMass + MassDiffDictionary.NitrogenMass;

                        // must query
                        var queryMust = new List<Peak> {
                                new Peak() { Mz = sph2, Intensity = 5 },
                            };
                        var foundCountMust = 0;
                        var averageIntensityMust = 0.0;
                        countFragmentExistence(spectrum, queryMust, ms2Tolerance, out foundCountMust, out averageIntensityMust);
                        if (foundCountMust == 0) continue;

                        var query = new List<Peak> {
                                new Peak() { Mz = sph1, Intensity = 0.01 },
                                new Peak() { Mz = sph3, Intensity = 0.01 },
                            };
                        var foundCount = 0;
                        var averageIntensity = 0.0;
                        countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);
                        var foundCountThresh = acylCarbon < 12 ? 2 : 1; // to exclude strange annotation

                        if (foundCount >= foundCountThresh)
                        { // 
                                var molecule = getCeramideMoleculeObjAsLevel2("Cer", LbmClass.Cer_NDS, "d", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, averageIntensity);
                                candidates.Add(molecule);
                            }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("Cer-NDS", LbmClass.Cer_NDS, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}
                    if (candidates.Count == 0) return null;

                    return returnAnnotationResult("Cer", LbmClass.Cer_NDS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M-H]-" || adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    //var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M-H]-" ? theoreticalMz :
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                    // seek [M-CH2-H2O-H]-
                    var threshold1 = 1.0;
                    var diagnosticMz1 = diagnosticMz - 12 - H2O - 2 * MassDiffDictionary.HydrogenMass;
                    // seek [M-CH2O-H2O-H]-
                    var threshold2 = 1.0;
                    var diagnosticMz2 = diagnosticMz1 - MassDiffDictionary.OxygenMass;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon1Found != true || isClassIon2Found != true) return null;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz3 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold3 = 50.0;
                        var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                        if (isClassIon3Found) return null;
                    }
                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++) {
                        int sphDouble = 0; // Cer-NDS sphingo chain don't have double bond

                        var acylCarbon = totalCarbon - sphCarbon;
                        //if (acylCarbon < minSphCarbon) { break; }
                        var acylDouble = totalDoubleBond - sphDouble;

                        var sphChain_loss = diagnosticMz - ((sphCarbon - 2) * 12) - (MassDiffDictionary.HydrogenMass * ((sphCarbon - 2) * 2 - sphDouble * 2 + 1)) -
                                 2 * MassDiffDictionary.OxygenMass - MassDiffDictionary.HydrogenMass; // as [FA+NCC-3H]- 
                        var sphFragment = ((sphCarbon - 2) * 12) + (MassDiffDictionary.HydrogenMass * ((sphCarbon - 2) * 2 - sphDouble * 2) - 1) + MassDiffDictionary.OxygenMass; // [Sph-NCC-3H]-
                        var acylFragment = fattyacidProductIon(acylCarbon, acylDouble) - MassDiffDictionary.OxygenMass - 2 * MassDiffDictionary.HydrogenMass; // 
                        var query = new List<Peak> {
                                new Peak() { Mz = sphChain_loss, Intensity = 5 },
                                new Peak() { Mz = sphFragment, Intensity = 1 },
                                new Peak() { Mz = acylFragment, Intensity = 1 }
                            };

                        var foundCount = 0;
                        var averageIntensity = 0.0;
                        countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                        if (foundCount >= 2) {
                            var molecule = getCeramideMoleculeObjAsLevel2("Cer", LbmClass.Cer_NDS, "d", sphCarbon, sphDouble,
                                acylCarbon, acylDouble, averageIntensity);
                            candidates.Add(molecule);
                        }

                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("Cer-NDS", LbmClass.Cer_NDS, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}

                    return returnAnnotationResult("Cer", LbmClass.Cer_NDS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfHexceramidens(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {
                    // seek -H2O
                    var threshold = 1.0;
                    var diagnosticMz = theoreticalMz - H2O;
                    // seek -H2O -Hex(-C6H10O5)
                    var threshold2 = 1.0;
                    var diagnosticMz2 = diagnosticMz - 162.052833;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIonFound == !true|| isClassIon2Found == !true) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) // sphingo chain must have double bond
                        {

                            var acylCarbon = totalCarbon - sphCarbon;
                           // if (acylCarbon < minSphCarbon) { break; }
                            var acylDouble = totalDoubleBond - sphDouble;

                            var sph1 = diagnosticMz2 - acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.HydrogenMass;
                            var sph2 = sph1 - H2O;
                            var sph3 = sph2 - 12; //[Sph-CH4O2+H]+

                            var acylamide = acylCarbon * 12 + (((2 * acylCarbon) - (2 * acylDouble) + 2) * MassDiffDictionary.HydrogenMass) + MassDiffDictionary.OxygenMass + MassDiffDictionary.NitrogenMass;

                            var query = new List<Peak> {
                                new Peak() { Mz = sph1, Intensity = 0.01 },
                                new Peak() { Mz = sph2, Intensity = 0.01 },
                                new Peak() { Mz = sph3, Intensity = 0.01 },
                                //new Peak() { Mz = acylamide, Intensity = 0.01 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2)
                            { // 
                                var molecule = getCeramideMoleculeObjAsLevel2("HexCer", LbmClass.HexCer_NS, "d", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("HexCer-NS", LbmClass.HexCer_NS, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}
                    return returnAnnotationResult("HexCer", LbmClass.HexCer_NS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M-H]-" || adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    //var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M-H]-" ? theoreticalMz :
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                    // seek [[M-C6H10O5-H]-
                    var threshold1 = 10.0;
                    var diagnosticMz1 = diagnosticMz - 162.052833;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    if (isClassIonFound != true ) return null;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz3 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold3 = 50.0;
                        var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                        if (isClassIon3Found) return null;
                    }

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++) {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) {

                            var acylCarbon = totalCarbon - sphCarbon;
                            //if (acylCarbon < minSphCarbon) { break; }
                            var acylDouble = totalDoubleBond - sphDouble;

                            var sphChain_loss = diagnosticMz1 - ((sphCarbon - 2) * 12) - (MassDiffDictionary.HydrogenMass * ((sphCarbon - 2) * 2 - sphDouble * 2 + 1)) -
                                     2 * MassDiffDictionary.OxygenMass - MassDiffDictionary.HydrogenMass; // as [FA+NCCO-3H]- 
                            var sphFragment = ((sphCarbon - 2) * 12) + (MassDiffDictionary.HydrogenMass * ((sphCarbon - 2) * 2 - sphDouble * 2) - 1) + MassDiffDictionary.OxygenMass; // [Sph-NCC-3H]-
                            //var acylamide = acylCarbon * 12 + (((2 * acylCarbon) - (2 * acylDouble) - 1) * MassDiffDictionary.HydrogenMass) + MassDiffDictionary.NitrogenMass;
                            var acylFragment = fattyacidProductIon(acylCarbon, acylDouble) - MassDiffDictionary.OxygenMass - 2 * MassDiffDictionary.HydrogenMass; // 

                            var query = new List<Peak> {
                                new Peak() { Mz = sphChain_loss, Intensity = 0.1 },
                                new Peak() { Mz = sphFragment, Intensity = 0.1 },
                                new Peak() { Mz = acylFragment, Intensity = 0.1 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2) {
                                { // 
                                    var molecule = getCeramideMoleculeObjAsLevel2("HexCer", LbmClass.HexCer_NS, "d", sphCarbon, sphDouble,
                                        acylCarbon, acylDouble, averageIntensity);
                                    candidates.Add(molecule);
                                }
                            }
                        }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("HexCer-NS", LbmClass.HexCer_NS, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}

                    return returnAnnotationResult("HexCer", LbmClass.HexCer_NS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfHexceramideo(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
            AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+") {
                    // seek -H2O
                    var threshold = 1.0;
                    var diagnosticMz = theoreticalMz - H2O;
                    // seek -H2O -Hex(-C6H10O5)
                    var threshold2 = 1.0;
                    var diagnosticMz2 = diagnosticMz - 162.052833;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIonFound == !true || isClassIon2Found == !true) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++) {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) // sphingo chain must have double bond
                        {

                            var acylCarbon = totalCarbon - sphCarbon;
                            // if (acylCarbon < minSphCarbon) { break; }
                            var acylDouble = totalDoubleBond - sphDouble;

                            var sph1 = diagnosticMz2 - acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.HydrogenMass - MassDiffDictionary.OxygenMass;
                            var sph2 = sph1 - H2O;
                            var sph3 = sph2 - 12; //[Sph-CH4O2+H]+

                            var acylamide = acylCarbon * 12 + (((2 * acylCarbon) - (2 * acylDouble) + 2) * MassDiffDictionary.HydrogenMass) + MassDiffDictionary.OxygenMass + MassDiffDictionary.NitrogenMass;

                            //Console.WriteLine("HexCer-O" + sphCarbon + ":" + sphDouble + "/" + acylCarbon + ":" + acylDouble + " " +
                            //    sph1 + " " + sph2 + " " + sph3);

                            var query = new List<Peak> {
                                new Peak() { Mz = sph1, Intensity = 0.01 },
                                new Peak() { Mz = sph2, Intensity = 0.01 },
                                new Peak() { Mz = sph3, Intensity = 0.01 },
                                //new Peak() { Mz = acylamide, Intensity = 0.01 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2) { // 

                                //var header = sphDouble == 0 ? "HexCer-HDS" : "HexCer-HS";
                                var header = "HexCer";
                                var lbm = sphDouble == 0 ? LbmClass.HexCer_HDS : LbmClass.HexCer_HS;

                                var molecule = getCeramideoxMoleculeObjAsLevel2(header, lbm, "d", sphCarbon, sphDouble,
                                            acylCarbon, acylDouble, 1, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }

                    //var headerString = totalDoubleBond == 0 ? "HexCer-HDS" : "HexCer-HS";
                    var headerString =  "HexCer";
                    var lbmClass = totalDoubleBond == 0 ? LbmClass.HexCer_HDS : LbmClass.HexCer_HS;

                    return returnAnnotationResult(headerString, lbmClass, "d", theoreticalMz, adduct,
                            totalCarbon, totalDoubleBond, 1, candidates, 2);
                }
            }
            else {
                if (adduct.AdductIonName == "[M-H]-" || adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-") {
                    // calc [M-H]-
                    //var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M-H]-" ? theoreticalMz :
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                    // seek [[M-C6H10O5-H]-
                    var threshold1 = 5.0;
                    var diagnosticMz1 = diagnosticMz - 162.052833;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    if (isClassIonFound != true) return null;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz3 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold3 = 50.0;
                        var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                        if (isClassIon3Found) return null;
                    }

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++) {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) {

                            var acylCarbon = totalCarbon - sphCarbon;
                            var acylDouble = totalDoubleBond - sphDouble;
                            var acylFragment = fattyacidProductIon(acylCarbon, acylDouble) + MassDiffDictionary.NitrogenMass - MassDiffDictionary.HydrogenMass; // 
                            //Console.WriteLine("HexCer-O " + sphCarbon + ":" + sphDouble + "/" + acylCarbon + ":" + acylDouble + " " + acylFragment);
                            var query = new List<Peak> {
                                new Peak() { Mz = acylFragment, Intensity = 0.1 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 1) {
                                { // 
                                    //var header = sphDouble == 0 ? "HexCer-HDS" : "HexCer-HS";
                                    var header = "HexCer";
                                    var lbm = sphDouble == 0 ? LbmClass.HexCer_HDS : LbmClass.HexCer_HS;

                                    var molecule = getCeramideoxMoleculeObjAsLevel2(header, lbm, "d", sphCarbon, sphDouble,
                                             acylCarbon, acylDouble, 1, averageIntensity);
                                    candidates.Add(molecule);
                                }
                            }
                        }
                    }

                    //var headerString = totalDoubleBond == 0 ? "HexCer-HDS" : "HexCer-HS";
                    var headerString=  "HexCer";
                    var lbmClass = totalDoubleBond == 0 ? LbmClass.HexCer_HDS : LbmClass.HexCer_HS;

                    return returnAnnotationResult(headerString, lbmClass, "d", theoreticalMz, adduct,
                            totalCarbon, totalDoubleBond, 1, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfHexceramidends(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {
                    // seek -H2O
                    //var threshold = 1.0;
                    var diagnosticMz = theoreticalMz - H2O;
                    // seek -H2O -Hex(-C6H10O5)
                    var threshold2 = 1.0;
                    var diagnosticMz2 = diagnosticMz - 162.052833;
                    //var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon2Found == !true) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        int sphDouble = 0; // Cer-NDS sphingo chain don't have double bond

                        var acylCarbon = totalCarbon - sphCarbon;
                        //if (acylCarbon < minSphCarbon) { break; }
                        var acylDouble = totalDoubleBond - sphDouble;

                        var sph1 = diagnosticMz2 - acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.HydrogenMass;
                        var sph2 = sph1 - H2O;
                        // var sph3 = sph2 - 12; //[Sph-CH4O2+H]+ (may be not found)
                        var acylamide = acylCarbon * 12 + (((2 * acylCarbon) - (2 * acylDouble) + 2) * MassDiffDictionary.HydrogenMass) + MassDiffDictionary.OxygenMass + MassDiffDictionary.NitrogenMass;

                        var query = new List<Peak> {
                                new Peak() { Mz = sph1, Intensity = 0.01 },
                                new Peak() { Mz = sph2, Intensity = 0.01 },
                                //new Peak() { Mz = acylamide, Intensity = 0.01 }
                            };

                        var foundCount = 0;
                        var averageIntensity = 0.0;
                        countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                        if (foundCount == 2)
                        { // 
                            var molecule = getCeramideMoleculeObjAsLevel2("HexCer", LbmClass.HexCer_NDS, "d", sphCarbon, sphDouble,
                                acylCarbon, acylDouble, averageIntensity);
                            candidates.Add(molecule);
                        }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("HexCer-NDS", LbmClass.HexCer_NDS, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}

                    return returnAnnotationResult("HexCer", LbmClass.HexCer_NDS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M-H]-" || adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    //var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M-H]-" ? theoreticalMz :
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                    // seek [[M-C6H10O5-H]-
                    var threshold = 10.0;
                    var diagnosticMz1 = diagnosticMz - 162.052833;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold);
                    if (isClassIonFound != true) return null;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz3 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold3 = 50.0;
                        var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                        if (isClassIon3Found) return null;
                    }

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        int sphDouble = 0; // HexCer-NDS sphingo chain don't have double bond

                        var acylCarbon = totalCarbon - sphCarbon;
                        //if (acylCarbon < minSphCarbon) { break; }
                        var acylDouble = totalDoubleBond - sphDouble;

                        var sphChain_loss = diagnosticMz1 - ((sphCarbon - 2) * 12) - (MassDiffDictionary.HydrogenMass * ((sphCarbon - 2) * 2 - sphDouble * 2 + 1)) -
                                 2 * MassDiffDictionary.OxygenMass - MassDiffDictionary.HydrogenMass; // as [FA+NCCO-3H]- 
                        var sphFragment = ((sphCarbon - 2) * 12) + (MassDiffDictionary.HydrogenMass * ((sphCarbon - 2) * 2 - sphDouble * 2) - 1) + MassDiffDictionary.OxygenMass; // [Sph-NCC-3H]-
                        //var acylamide = acylCarbon * 12 + (((2 * acylCarbon) - (2 * acylDouble) - 1) * MassDiffDictionary.HydrogenMass) + MassDiffDictionary.NitrogenMass;
                        var acylFragment = fattyacidProductIon(acylCarbon, acylDouble) - MassDiffDictionary.OxygenMass - 2 * MassDiffDictionary.HydrogenMass; // 

                        var query = new List<Peak> {
                                new Peak() { Mz = sphChain_loss, Intensity = 0.1 },
                                new Peak() { Mz = sphFragment, Intensity = 0.1 },
                                new Peak() { Mz = acylFragment, Intensity = 0.1 }
                            };

                        var foundCount = 0;
                        var averageIntensity = 0.0;
                        countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                        if (foundCount >= 2)
                        { // the diagnostic acyl ion must be observed for level 2 annotation
                            var molecule = getCeramideMoleculeObjAsLevel2("HexCer", LbmClass.HexCer_NDS, "d", sphCarbon, sphDouble,
                                acylCarbon, acylDouble, averageIntensity);
                            candidates.Add(molecule);
                        }

                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("HexCer-NDS", LbmClass.HexCer_NDS, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}

                    return returnAnnotationResult("HexCer", LbmClass.HexCer_NDS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfCeramideo(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
           int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
           AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+") {
                    // seek -H2O
                    var threshold = 5.0;
                    var diagnosticMz = theoreticalMz - H2O;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++) {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) // sphingo chain must have double bond
                        {
                            var acylCarbon = totalCarbon - sphCarbon;
                            var acylDouble = totalDoubleBond - sphDouble;

                            var sph1 = diagnosticMz - acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.HydrogenMass - MassDiffDictionary.OxygenMass;
                            var sph2 = sph1 - H2O;
                            var sph3 = sph2 - 12; //[Sph-CH4O2+H]+
                            var acylamide = acylCarbon * 12 + (((2 * acylCarbon) - (2 * acylDouble) + 2) * MassDiffDictionary.HydrogenMass) + MassDiffDictionary.OxygenMass + MassDiffDictionary.NitrogenMass;

                            var query = new List<Peak> {
                                new Peak() { Mz = sph1, Intensity = 0.01 },
                                new Peak() { Mz = sph2, Intensity = 0.01 },
                                new Peak() { Mz = sph3, Intensity = 0.01 },
                                //new Peak() { Mz = acylamide, Intensity = 0.01 }
                            };

                            //Console.WriteLine("Cer-O" + sphCarbon + ":" + sphDouble + "/" + acylCarbon + ":" + acylDouble + " " +
                            // sph1 + " " + sph2 + " " + sph3);


                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2) { // 
                                //var header = sphDouble == 0 ? "Cer-HDS" : "Cer-HS";
                                var header = "Cer";
                                var lbm = sphDouble == 0 ? LbmClass.Cer_HDS : LbmClass.Cer_HS;

                                var molecule = getCeramideoxMoleculeObjAsLevel2(header, lbm, "d", sphCarbon, sphDouble,
                                             acylCarbon, acylDouble, 1, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (candidates.Count == 0) return null;

                    //var headerString = totalDoubleBond == 0 ? "Cer-HDS" : "Cer-HS";
                    var headerString = "Cer";
                    var lbmClass = totalDoubleBond == 0 ? LbmClass.Cer_HDS : LbmClass.Cer_HS;

                    return returnAnnotationResult(headerString, lbmClass, "d", theoreticalMz, adduct,
                             totalCarbon, totalDoubleBond, 1, candidates, 2);

                }
            }
            else if (adduct.IonMode == IonMode.Negative) { // negative ion mode 
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-") {
                    // calc [M-H]-
                    //var threshold = 10.0;
                    var diagnosticMz =
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                    // seek [M-CH2O-H]-
                    var threshold1 = 0.1;
                    var diagnosticMz1 = diagnosticMz - H2O - 12;
                    // seek [M-CH4O2-H]-
                    var threshold2 = 0.1;
                    var diagnosticMz2 = diagnosticMz1 - H2O;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon1Found != true && isClassIon2Found != true) return null;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz5 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold5 = 50.0;
                        var isClassIon5Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz5, threshold5);
                        if (isClassIon5Found) return null;
                        var diagnosticMz6 = diagnosticMz;
                        var threshold6 = 5;
                        var isClassIon6Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz6, threshold6);
                        if (!isClassIon6Found) return null;
                    }
                    else if (adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+FA-H]-") {
                        var diagnosticMz6 = diagnosticMz;
                        var threshold6 = 20.0;
                        var isClassIon6Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz6, threshold6);
                        if (!isClassIon6Found) return null;
                    }


                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++) {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) {
                            var acylCarbon = totalCarbon - sphCarbon;
                            //if (acylCarbon < minSphCarbon) { break; }
                            var acylDouble = totalDoubleBond - sphDouble;

                            var sphChain_loss = diagnosticMz - ((sphCarbon - 2) * 12) - (MassDiffDictionary.HydrogenMass * ((sphCarbon - 2) * 2 - sphDouble * 2 + 1)) -
                                     2 * MassDiffDictionary.OxygenMass - MassDiffDictionary.HydrogenMass; // as [FA+C2H3N]- 
                            var sphFragment = SphingoChainMass(sphCarbon - 2, sphDouble) - MassDiffDictionary.OxygenMass - MassDiffDictionary.NitrogenMass - Proton; // [Sph-NCC-3H]-
                            var acylFragment = fattyacidProductIon(acylCarbon, acylDouble) + MassDiffDictionary.OxygenMass;
                            var query = new List<Peak> {
                                    new Peak() { Mz = sphChain_loss, Intensity = 0.1 },
                                    new Peak() { Mz = sphFragment, Intensity = 1 },
                                    new Peak() { Mz = acylFragment, Intensity = 1 }
                                };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2) { // the diagnostic acyl ion must be observed for level 2 annotation
                                var acylOxidized = 1; //
                                var molecule = getCeramideoxMoleculeObjAsLevel2("Cer", LbmClass.Cer_HS, "d", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, acylOxidized, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }

                    return returnAnnotationResult("Cer", LbmClass.Cer_HS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 1, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfCeramidedos(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
           int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
           AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+") {
                    // seek -H2O
                    var threshold = 5.0;
                    var diagnosticMz = theoreticalMz - H2O;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++) {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) // sphingo chain must have double bond
                        {
                            var acylCarbon = totalCarbon - sphCarbon;
                            var acylDouble = totalDoubleBond - sphDouble;

                            var sph1 = diagnosticMz - acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.HydrogenMass + MassDiffDictionary.OxygenMass;
                            var sph2 = sph1 - H2O;
                            var sph3 = sph2 - 12; //[Sph-CH4O2+H]+
                            var acylamide = acylCarbon * 12 + (((2 * acylCarbon) - (2 * acylDouble) + 2) * MassDiffDictionary.HydrogenMass) + MassDiffDictionary.OxygenMass + MassDiffDictionary.NitrogenMass;

                            var query = new List<Peak> {
                                new Peak() { Mz = sph1, Intensity = 0.01 },
                                new Peak() { Mz = sph2, Intensity = 0.01 },
                                new Peak() { Mz = sph3, Intensity = 0.01 },
                                //new Peak() { Mz = acylamide, Intensity = 0.01 }
                            };

                            //Console.WriteLine("Cer-DOS" + sphCarbon + ":" + sphDouble + "/" + acylCarbon + ":" + acylDouble + " " +
                            // sph1 + " " + sph2 + " " + sph3);

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2) { // 
                                var molecule = getCeramideMoleculeObjAsLevel2("Cer", LbmClass.Cer_NDOS, "m", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (candidates.Count == 0) return null;

                    return returnAnnotationResult("Cer", LbmClass.Cer_NDOS, "m", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);

                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfHexhexceramidens(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            //if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            //if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            { // negative ion mode 
              if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    var threshold1 = 10.0;
                    var diagnosticMz1 = adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                    // seek [M-C6H10O5-H]-
                    var threshold2 = 5;
                    var diagnosticMz2 = diagnosticMz1 - 162.052833;
                    // seek [M-C12H20O10-H]-
                    var threshold3 = 1;
                    var diagnosticMz3 = diagnosticMz2 - 162.052833;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                    if (isClassIon1Found != true || isClassIon2Found != true || isClassIon3Found != true) return null;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz4 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold4 = 50.0;
                        var isClassIon4Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz4, threshold4);
                        if (isClassIon4Found) return null;
                    }

                    // from here, acyl level annotation is executed.
                    //   may be not found fragment to define sphingo and acyl chain
                    var candidates = new List<LipidMolecule>();
                    //var score = 0;
                    //var molecule0 = getCeramideMoleculeObjAsLevel2_0("HexHexCer-NS", LbmClass.HexHexCer_NS, "d", totalCarbon, totalDoubleBond,
                    //    score);
                    //candidates.Add(molecule0);
 
                    return returnAnnotationResult("Hex2Cer", LbmClass.Hex2Cer, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            else {
                if (adduct.AdductIonName == "[M+H]+") {
                    // seek -H2O
                    var threshold = 1.0;
                    var diagnosticMz = theoreticalMz - H2O;
                    // seek -H2O -Hex(-C6H10O5)
                    var threshold2 = 1.0;
                    var diagnosticMz2 = diagnosticMz - 162.052833;

                    var threshold3 = 1.0;
                    var diagnosticMz3 = diagnosticMz2 - 162.052833;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);

                    if (!isClassIonFound || !isClassIon2Found || !isClassIon3Found) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++) {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) // sphingo chain must have double bond
                        {

                            var acylCarbon = totalCarbon - sphCarbon;
                            var acylDouble = totalDoubleBond - sphDouble;

                            var sph1 = diagnosticMz3 - acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.HydrogenMass;
                            var sph2 = sph1 - H2O;
                            var sph3 = sph2 - 12; //[Sph-CH4O2+H]+

                            var acylamide = acylCarbon * 12 + (((2 * acylCarbon) - (2 * acylDouble) + 2) * MassDiffDictionary.HydrogenMass) + MassDiffDictionary.OxygenMass + MassDiffDictionary.NitrogenMass;

                            var query = new List<Peak> {
                                new Peak() { Mz = sph2, Intensity = 1 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 1) { // 
                                var molecule = getCeramideMoleculeObjAsLevel2("Hex2Cer", LbmClass.Hex2Cer, "d", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    return returnAnnotationResult("Hex2Cer", LbmClass.Hex2Cer, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfHexhexhexceramidens(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            { // negative ion mode 
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                      adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    var threshold1 = 10.0;
                    var diagnosticMz1 = adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                    // seek [M-C6H10O5-H]-
                    var threshold2 = 1;
                    var diagnosticMz2 = diagnosticMz1 - 162.052833;
                    // seek [M-C12H20O10-H]-
                    var threshold3 = 1;
                    var diagnosticMz3 = diagnosticMz2 - 162.052833;
                    // seek [M-C18H30O15-H]-
                    var threshold4 = 1;
                    var diagnosticMz4 = diagnosticMz3 - 162.052833;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                    var isClassIon4Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz4, threshold4);

                    if (isClassIon1Found != true || isClassIon2Found != true || isClassIon3Found != true || isClassIon4Found != true) return null;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz5 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold5 = 50.0;
                        var isClassIon5Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz5, threshold5);
                        if (isClassIon5Found) return null;
                    }

                    // from here, acyl level annotation is executed.
                    //   may be not found fragment to define sphingo and acyl chain
                    var candidates = new List<LipidMolecule>();
                    //var score = 0;
                    //var molecule0 = getCeramideMoleculeObjAsLevel2_0("HexHexHexCer_NS", LbmClass.HexHexHexCer_NS, "d", totalCarbon, totalDoubleBond,
                    //    score);
                    //candidates.Add(molecule0);

                    return returnAnnotationResult("Hex3Cer", LbmClass.Hex3Cer, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            else {
                if (adduct.AdductIonName == "[M+H]+") {
                    // seek -H2O
                    var threshold = 1.0;
                    var diagnosticMz = theoreticalMz - H2O;
                    // seek -H2O -Hex(-C6H10O5)
                    var threshold2 = 1.0;
                    var diagnosticMz2 = diagnosticMz - 162.052833;

                    var threshold3 = 1.0;
                    var diagnosticMz3 = diagnosticMz2 - 162.052833;

                    var threshold4 = 1.0;
                    var diagnosticMz4 = diagnosticMz3 - 162.052833;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                    var isClassIon4Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz4, threshold4);

                    if (!isClassIonFound || !isClassIon2Found || !isClassIon3Found || !isClassIon4Found) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++) {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) // sphingo chain must have double bond
                        {

                            var acylCarbon = totalCarbon - sphCarbon;
                            var acylDouble = totalDoubleBond - sphDouble;

                            var sph1 = diagnosticMz4 - acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.HydrogenMass;
                            var sph2 = sph1 - H2O;
                            var sph3 = sph2 - 12; //[Sph-CH4O2+H]+

                            var acylamide = acylCarbon * 12 + (((2 * acylCarbon) - (2 * acylDouble) + 2) * MassDiffDictionary.HydrogenMass) + MassDiffDictionary.OxygenMass + MassDiffDictionary.NitrogenMass;

                            var query = new List<Peak> {
                                new Peak() { Mz = sph2, Intensity = 1 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 1) { // 
                                var molecule = getCeramideMoleculeObjAsLevel2("Hex3Cer", LbmClass.Hex3Cer, "d", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    return returnAnnotationResult("Hex3Cer", LbmClass.Hex3Cer, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfCeramideap(ObservableCollection<double[]> spectrum, double ms2Tolerance,
        double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
        int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
        AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {
                    // seek -H2O
                    var threshold = 1.0;
                    var diagnosticMz = theoreticalMz - H2O;
                    // seek -2H2O
                    var threshold2 = 1.0;
                    var diagnosticMz2 = diagnosticMz - H2O;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIonFound == !true || isClassIon2Found == !true) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) 
                        {
                            var acylCarbon = totalCarbon - sphCarbon;
                            //if (acylCarbon < minSphCarbon) { break; }
                            var acylDouble = totalDoubleBond - sphDouble;

                            var sph1 = theoreticalMz - acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.HydrogenMass - MassDiffDictionary.OxygenMass;
                            var sph2 = sph1 - H2O;
                            var sph3 = sph1 - 2 * H2O;
                            var sph4 = sph1 - 3 * H2O;
                            var acylamide = acylCarbon * 12 + (((2 * acylCarbon) - (2 * acylDouble) + 2) * MassDiffDictionary.HydrogenMass) + 2 * MassDiffDictionary.OxygenMass + MassDiffDictionary.NitrogenMass;

                            var query = new List<Peak> {
                                new Peak() { Mz = sph1, Intensity = 1 },
                                new Peak() { Mz = sph2, Intensity = 1 },
                                new Peak() { Mz = sph3, Intensity = 1 },
                                new Peak() { Mz = sph4, Intensity = 1 },
                                //new Peak() { Mz = acylamide, Intensity = 0.01 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2)
                            { // 
                                var molecule = getCeramideoxMoleculeObjAsLevel2("Cer", LbmClass.Cer_AP, "t", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, 1, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    return returnAnnotationResult("Cer", LbmClass.Cer_AP, "t", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 1, candidates, 2);

                }
            }
            else
            {
                if (adduct.AdductIonName == "[M-H]-" || adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    //var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M-H]-" ? theoreticalMz :
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz2 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold2 = 50.0;
                        var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                        if (isClassIon2Found) return null;
                    }
                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++)
                        {

                            var acylCarbon = totalCarbon - sphCarbon;
                            //if (acylCarbon < minSphCarbon) { break; }
                            var acylDouble = totalDoubleBond - sphDouble;

                            var sphChain_loss = diagnosticMz - ((sphCarbon - 3) * 12) - (MassDiffDictionary.HydrogenMass * ((sphCarbon - 3) * 2 - sphDouble * 2 + 1)) -
                                     2 * MassDiffDictionary.OxygenMass - MassDiffDictionary.HydrogenMass; // 
                            var sphFragment = ((sphCarbon - 2) * 12) + (MassDiffDictionary.HydrogenMass * ((sphCarbon - 2) * 2 - sphDouble * 2) - 1) + 2 * MassDiffDictionary.OxygenMass; // [Sph-NCC-2H2O]-
                            var acylFragment1 = fattyacidProductIon(acylCarbon, acylDouble) + MassDiffDictionary.OxygenMass; // FA(+OH)+ O (may be not sure)
                            var acylFragment2 = fattyacidProductIon(acylCarbon, acylDouble) - 12 - MassDiffDictionary.OxygenMass - 2 * MassDiffDictionary.HydrogenMass; // FA(+OH) -C -O -2H(may be not sure)

                            //Console.WriteLine("d" + sphCarbon + ":" + sphDouble + "/" + acylCarbon + ":" + acylDouble + " " +
                            //    sphChain_loss + " " + sphFragment + " " + acylFragment1 + " " + acylFragment2);

                            var query = new List<Peak> {
                                new Peak() { Mz = sphChain_loss, Intensity = 1 },
                                new Peak() { Mz = sphFragment, Intensity = 0.1 },
                                new Peak() { Mz = acylFragment1, Intensity = 0.01 },
                                new Peak() { Mz = acylFragment2, Intensity = 0.01 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 3)
                            { // the diagnostic acyl ion must be observed for level 2 annotation
                                var molecule = getCeramideoxMoleculeObjAsLevel2("Cer", LbmClass.Cer_AP, "t", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, 1, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (candidates.Count == 0) return null;

                    return returnAnnotationResult("Cer", LbmClass.Cer_AP, "t", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 1, candidates, 2);
                }

            }
            return null;
        }

        public static LipidMolecule JudgeIfHexceramideap(ObservableCollection<double[]> spectrum, double ms2Tolerance,
        double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
        int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
        AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {
                    // seek -Hex(-C6H10O5)
                    var threshold = 1.0;
                    var diagnosticMz = theoreticalMz - 162.052833 ;
                    // seek -H2O -Hex(-C6H10O5)
                    var threshold2 = 1.0;
                    var diagnosticMz2 = diagnosticMz - H2O;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIonFound == !true || isClassIon2Found == !true) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++)
                        {
                            var acylCarbon = totalCarbon - sphCarbon;
                            var acylDouble = totalDoubleBond - sphDouble;
                            if (sphCarbon >= 24 || sphCarbon <= 14) continue;

                            var sph1 = diagnosticMz - acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.HydrogenMass - MassDiffDictionary.OxygenMass;
                            var sph2 = sph1 - H2O;
                            var sph3 = sph1 - 2 * H2O;
                            var sph4 = sph1 - 3 * H2O;
                            var acylamide = acylCarbon * 12 + (((2 * acylCarbon) - (2 * acylDouble) + 2) * MassDiffDictionary.HydrogenMass) + 2 * MassDiffDictionary.OxygenMass + MassDiffDictionary.NitrogenMass;

                            var query = new List<Peak> {
                                new Peak() { Mz = sph1, Intensity = 1 },
                                new Peak() { Mz = sph2, Intensity = 1 },
                                new Peak() { Mz = sph3, Intensity = 1 },
                                new Peak() { Mz = sph4, Intensity = 1 },
                                //new Peak() { Mz = acylamide, Intensity = 0.01 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2)
                            { // 
                                var molecule = getCeramideoxMoleculeObjAsLevel2("HexCer", LbmClass.HexCer_AP, "t", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, 1, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("HexCer-AP", LbmClass.HexCer_AP, "t", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}

                    //return returnAnnotationResult("HexCer-AP", LbmClass.HexCer_AP, "t", theoreticalMz, adduct,
                    //    totalCarbon, totalDoubleBond, 0, candidates);
                    return returnAnnotationResult("HexCer", LbmClass.HexCer_AP, "t", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 1, candidates, 2);
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M-H]-" || adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    //var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M-H]-" ? theoreticalMz :
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                    // seek [M-C6H10O5-H]-
                    var threshold = 1.0;
                    var diagnosticMz1 = diagnosticMz - 162.052833;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold);
                    if (isClassIonFound != true) return null;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz5 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold5 = 50.0;
                        var isClassIon5Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz5, threshold5);
                        if (isClassIon5Found) return null;
                    }


                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++)
                        {
                            var acylCarbon = totalCarbon - sphCarbon;
                            //if (acylCarbon < minSphCarbon) { break; }
                            var acylDouble = totalDoubleBond - sphDouble;

                            var sphChain_loss = diagnosticMz1 - ((sphCarbon - 3) * 12) - (MassDiffDictionary.HydrogenMass * ((sphCarbon - 3) * 2 - sphDouble * 2 + 1)) -
                                     2 * MassDiffDictionary.OxygenMass - MassDiffDictionary.HydrogenMass; // 
                            var acylFragment1 = fattyacidProductIon(acylCarbon, acylDouble) + MassDiffDictionary.OxygenMass; // FA(+OH)+ O (may be not sure)
                            var acylFragment2 = fattyacidProductIon(acylCarbon, acylDouble) - 12 - MassDiffDictionary.OxygenMass - 2 * MassDiffDictionary.HydrogenMass; // FA(+OH) -C -O -2H(may be not sure)
                            var query = new List<Peak> {
                                new Peak() { Mz = sphChain_loss, Intensity = 1 },
                                new Peak() { Mz = acylFragment1, Intensity = 0.1 },
                                new Peak() { Mz = acylFragment2, Intensity = 0.1 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2)
                            { // the diagnostic acyl ion must be observed for level 2 annotation
                                var molecule = getCeramideoxMoleculeObjAsLevel2("HexCer", LbmClass.HexCer_AP, "t", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, 1, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("HexCer-AP", LbmClass.HexCer_AP, "t", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}

                    return returnAnnotationResult("HexCer", LbmClass.HexCer_AP, "t", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 1, candidates, 2);
                }

            }
            return null;
        }

        public static LipidMolecule JudgeIfCeramideas(ObservableCollection<double[]> spectrum, double ms2Tolerance,
        double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
        int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
        AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative) { // negative ion mode 
                if (adduct.AdductIonName == "[M-H]-" || adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-") {
                    // calc [M-H]-
                    //var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M-H]-" ? theoreticalMz :
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                    // seek [M-H2O-H]-
                    var threshold1 = 1.0;
                    var diagnosticMz1 = diagnosticMz - H2O;
                    // seek [M-CH2O-H2O-H]-
                    var threshold2 = 1.0;
                    var diagnosticMz2 = diagnosticMz1 - H2O - 12;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon1Found != true && isClassIon2Found != true) return null;
                    var isSolventAdduct = false;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz5 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold5 = 50.0;
                        var isClassIon5Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz5, threshold5);
                        if (isClassIon5Found) return null;

                        var diagnosticMz6 = diagnosticMz;
                        var threshold6 = 5;
                        var isClassIon6Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz6, threshold6);
                        if (!isClassIon6Found) return null;

                        isSolventAdduct = true;
                    }
                    else if (adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+FA-H]-") {
                        var diagnosticMz6 = diagnosticMz;
                        var threshold6 = 20.0;
                        var isClassIon6Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz6, threshold6);
                        if (!isClassIon6Found) return null;

                        isSolventAdduct = true;
                    }

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++) {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) {

                            var acylCarbon = totalCarbon - sphCarbon;
                            //if (acylCarbon < minSphCarbon) { break; }
                            var acylDouble = totalDoubleBond - sphDouble;

                            var sphChain_loss = diagnosticMz - ((sphCarbon - 2) * 12) - (MassDiffDictionary.HydrogenMass * ((sphCarbon - 2) * 2 - sphDouble * 2 + 1)) -
                                     2 * MassDiffDictionary.OxygenMass - MassDiffDictionary.HydrogenMass; //
                            var sphFragment = ((sphCarbon - 2) * 12) + (MassDiffDictionary.HydrogenMass * ((sphCarbon - 2) * 2 - sphDouble * 2) - 1) + MassDiffDictionary.OxygenMass; // [Sph-NCCO-3H]-
                            var acylFragment = fattyacidProductIon(acylCarbon, acylDouble) - 12 - MassDiffDictionary.OxygenMass - 2 * MassDiffDictionary.HydrogenMass; // [FA-CO-3H]-
                            var query = new List<Peak> {
                                        new Peak() { Mz = sphChain_loss, Intensity = 0.5 },
                                        new Peak() { Mz = sphFragment, Intensity = 0.5 },
                                        new Peak() { Mz = acylFragment, Intensity = 0.5 }
                                };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2) { // the diagnostic acyl ion must be observed for level 2 annotation
                                var acylFragmentFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, acylFragment, 0.5);
                                if (acylFragmentFound == true) {
                                    var molecule = getCeramideoxMoleculeObjAsLevel2("Cer", LbmClass.Cer_AS, "d", sphCarbon, sphDouble,
                                        acylCarbon, acylDouble, 1, averageIntensity);
                                    candidates.Add(molecule);
                                }
                                else {
                                    var acylOxidized = 1; //case of cannot determine as alpha-OH
                                    var molecule = getCeramideoxMoleculeObjAsLevel2("Cer", LbmClass.Cer_HS, "d", sphCarbon, sphDouble,
                                        acylCarbon, acylDouble, acylOxidized, averageIntensity);
                                    candidates.Add(molecule);

                                }
                            }
                        }
                    }
                    if (candidates.Count == 0 && (!isClassIon1Found || !isClassIon2Found || isSolventAdduct == false)) return null;
                    return returnAnnotationResult("Cer", LbmClass.Cer_HS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 1, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfCeramideads(ObservableCollection<double[]> spectrum, double ms2Tolerance,
        double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
        int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
        AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            { // negative ion mode 
                if (adduct.AdductIonName == "[M-H]-" || adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    //var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M-H]-" ? theoreticalMz :
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                    // seek [M-H2O-H]-
                    var threshold1 = 1.0;
                    var diagnosticMz1 = diagnosticMz - H2O;
                    // seek [M-CH2O-H2O-H]-
                    var threshold2 = 1.0;
                    var diagnosticMz2 = diagnosticMz1 - H2O - 12;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon1Found != true && isClassIon2Found != true) return null;

                    var isSolventAdduct = false;
                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz5 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold5 = 50.0;
                        var isClassIon5Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz5, threshold5);
                        if (isClassIon5Found) return null;

                        var diagnosticMz6 = diagnosticMz;
                        var threshold6 = 5;
                        var isClassIon6Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz6, threshold6);
                        if (!isClassIon6Found) return null;
                        isSolventAdduct = true;
                    }
                    else if (adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+FA-H]-") {
                        var diagnosticMz6 = diagnosticMz;
                        var threshold6 = 20.0;
                        var isClassIon6Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz6, threshold6);
                        if (!isClassIon6Found) return null;
                        isSolventAdduct = true;
                    }

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        //if (sphCarbon >= 26) continue;
                        int sphDouble = 0; // Cer-ADS sphingo chain don't have double bond

                        var acylCarbon = totalCarbon - sphCarbon;
                           // if (acylCarbon < minSphCarbon) { break; }
                        var acylDouble = totalDoubleBond - sphDouble;

                        var sph1 = SphingoChainMass(sphCarbon, sphDouble) - MassDiffDictionary.HydrogenMass - Proton; // [Sph-CO+C=O-4H]-
                        var acylFragment = fattyacidProductIon(acylCarbon, acylDouble) - 2 * MassDiffDictionary.HydrogenMass; // 
                        var acylFragment2 = fattyacidProductIon(acylCarbon, acylDouble) - 2 * MassDiffDictionary.HydrogenMass - 12 - MassDiffDictionary.OxygenMass; // [FA-CO-3H]-
                        var query = new List<Peak> {
                                    new Peak() { Mz = sph1, Intensity = 1 },
                                    new Peak() { Mz = acylFragment, Intensity = 1 },
                                    new Peak() { Mz = acylFragment2, Intensity = 1 }
                                };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2)
                            { // the diagnostic acyl ion must be observed for level 2 annotation
                            var acylFragment2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, acylFragment2, 0.5);
                            if (acylFragment2Found == true)
                            {
                                var molecule = getCeramideoxMoleculeObjAsLevel2("Cer", LbmClass.Cer_ADS, "d", sphCarbon, sphDouble,
                                acylCarbon, acylDouble, 1, averageIntensity);
                                candidates.Add(molecule);
                            }
                            else
                            {
                                var acylOxidized = 1; //case of cannot determine as alpha-OH
                                var molecule = getCeramideoxMoleculeObjAsLevel2("Cer", LbmClass.Cer_HDS, "d", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, acylOxidized, averageIntensity);
                                candidates.Add(molecule);

                            }
                        }
                       
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("Cer-ADS", LbmClass.Cer_ADS, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}
                    if (candidates.Count == 0 && (!isClassIon1Found || !isClassIon2Found || isSolventAdduct == false)) return null;
                    return returnAnnotationResult("Cer", LbmClass.Cer_HDS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 1, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfCeramidebs(ObservableCollection<double[]> spectrum, double ms2Tolerance,
        double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
        int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
        AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            { // negative ion mode 
                if (adduct.AdductIonName == "[M-H]-" || adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    //var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M-H]-" ? theoreticalMz :
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                    // seek [M-H2O-H]-
                    var threshold1 = 1.0;
                    var diagnosticMz1 = diagnosticMz - H2O;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isSolventAdduct = false;
                    //if (isClassIon1Found != true) return null;
                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz5 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold5 = 50.0;
                        var isClassIon5Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz5, threshold5);
                        if (isClassIon5Found) return null;

                        var diagnosticMz6 = diagnosticMz;
                        var threshold6 = 5;
                        var isClassIon6Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz6, threshold6);
                        if (!isClassIon6Found) return null;

                        isSolventAdduct = true;
                    } else if (adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+FA-H]-") {
                        var diagnosticMz6 = diagnosticMz;
                        var threshold6 = 5;
                        var isClassIon6Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz6, threshold6);
                        if (!isClassIon6Found) return null;

                        isSolventAdduct = true;
                    }


                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++)
                        {
                            var acylCarbon = totalCarbon - sphCarbon;
                            //if (acylCarbon < minSphCarbon) { break; }
                            var acylDouble = totalDoubleBond - sphDouble;

                            var sph1 = SphingoChainMass(sphCarbon + 2, sphDouble) + MassDiffDictionary.OxygenMass + MassDiffDictionary.HydrogenMass - Proton; // [Sph+C2H2O-H]- suggest Cer-BS fragment
                            var sphFragment = SphingoChainMass(sphCarbon - 2, sphDouble) - MassDiffDictionary.OxygenMass - MassDiffDictionary.NitrogenMass - Proton; // [Sph-C2H7NO-H]-
                            var query = new List<Peak> {
                                    new Peak() { Mz = sph1, Intensity = 1 },
                                    new Peak() { Mz = sphFragment, Intensity = 1 },
                                };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { // the diagnostic acyl ion must be observed for level 2 annotation
                                var molecule = getCeramideoxMoleculeObjAsLevel2("Cer", LbmClass.Cer_BS, "d", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, 1, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("Cer-BS", LbmClass.Cer_BS, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}
                    if (candidates.Count == 0 && (!isClassIon1Found || isSolventAdduct == false)) return null;
                    return returnAnnotationResult("Cer", LbmClass.Cer_HS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 1, candidates, 2);
                }
            }
            return null;
        }
        public static LipidMolecule JudgeIfCeramidebds(ObservableCollection<double[]> spectrum, double ms2Tolerance,
        double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
        int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
        AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            { // negative ion mode 
                if (adduct.AdductIonName == "[M-H]-" || adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    //var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M-H]-" ? theoreticalMz :
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                    // seek [M-H2O-H]-  // maybe not found
                    //var threshold1 = 1.0;
                    //var diagnosticMz1 = diagnosticMz - H2O;

                    //var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    //if (isClassIon1Found != true ) return null;
                    var isSolventAdduct = false;
                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz5 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold5 = 50.0;
                        var isClassIon5Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz5, threshold5);
                        if (isClassIon5Found) return null;

                        var diagnosticMz6 = diagnosticMz;
                        var threshold6 = 5;
                        var isClassIon6Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz6, threshold6);
                        if (!isClassIon6Found) return null;

                        isSolventAdduct = true;
                    }
                    else if (adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+FA-H]-") {
                        var diagnosticMz6 = diagnosticMz;
                        var threshold6 = 20.0;
                        var isClassIon6Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz6, threshold6);
                        if (!isClassIon6Found) return null;

                        isSolventAdduct = true;
                    }

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        int sphDouble = 0; // Cer-BDS sphingo chain don't have double bond

                        var acylCarbon = totalCarbon - sphCarbon;
                        //if (acylCarbon < minSphCarbon) { break; }
                        var acylDouble = totalDoubleBond - sphDouble;

                        var sph1 = SphingoChainMass(sphCarbon + 2, sphDouble) + MassDiffDictionary.OxygenMass + MassDiffDictionary.HydrogenMass - Proton; // [Sph+C2H2O-H]- suggest beta-OH-FA fragment 
                        var sphFragment1 = SphingoChainMass(sphCarbon - 2, sphDouble) - MassDiffDictionary.OxygenMass - MassDiffDictionary.NitrogenMass - Proton; // [Sph-NCC-3H]-
                        var sphFragment2 = diagnosticMz - fattyacidProductIon(acylCarbon, acylDouble) -MassDiffDictionary.HydrogenMass + 12; // ([Sph+C=O-CO-3H]- on Excel sheet)

                        var mustQuery = new List<Peak> {
                                    new Peak() { Mz = sph1, Intensity = 50 },
                                };

                        var foundCount = 0;
                        var averageIntensity = 0.0;
                        countFragmentExistence(spectrum, mustQuery, ms2Tolerance, out foundCount, out averageIntensity);

                        if (foundCount != 1) continue;

                        var query = new List<Peak> {
                                    new Peak() { Mz = sph1, Intensity = 1 },
                                    new Peak() { Mz = sphFragment1, Intensity = 1 },
                                    new Peak() { Mz = sphFragment2, Intensity = 1 }
                                };

                        foundCount = 0;
                        averageIntensity = 0.0;
                        countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                        if (foundCount >= 2)
                        { // the diagnostic acyl ion must be observed for level 2 annotation
                            var molecule = getCeramideoxMoleculeObjAsLevel2("Cer", LbmClass.Cer_BDS, "d", sphCarbon, sphDouble,
                                acylCarbon, acylDouble, 1, averageIntensity);
                            candidates.Add(molecule);
                        }

                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("Cer-BDS", LbmClass.Cer_BDS, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}
                    if (candidates.Count == 0) return null;
                    return returnAnnotationResult("Cer", LbmClass.Cer_HDS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 1, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfCeramidenp(ObservableCollection<double[]> spectrum, double ms2Tolerance,
        double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
        int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
        AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            { // negative ion mode 
                if (adduct.AdductIonName == "[M-H]-" || adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    //var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M-H]-" ? theoreticalMz :
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                    // seek [M-H2O-H]-
                    var threshold1 = 0.10;
                    var diagnosticMz1 = diagnosticMz - H2O;
                    // seek [M-2H2O-H]-
                    var threshold2 = 0.10;
                    var diagnosticMz2 = diagnosticMz1 - H2O;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon1Found != true && isClassIon2Found != true) return null;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz5 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold5 = 50.0;
                        var isClassIon5Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz5, threshold5);
                        if (isClassIon5Found) return null;
                    }


                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++)
                        {

                            var acylCarbon = totalCarbon - sphCarbon;
                            //if (acylCarbon < minSphCarbon) { break; }
                            var acylDouble = totalDoubleBond - sphDouble;

                            var sphChain_loss = diagnosticMz - ((sphCarbon - 3) * 12) - (MassDiffDictionary.HydrogenMass * ((sphCarbon - 3) * 2 - sphDouble * 2 + 1)) -
                                     2 * MassDiffDictionary.OxygenMass - MassDiffDictionary.HydrogenMass; // [FAA+C3H5O-H]-
                            var sphFragment = ((sphCarbon - 2) * 12) + (MassDiffDictionary.HydrogenMass * ((sphCarbon - 2) * 2 - sphDouble * 2) - 1) + 2 * MassDiffDictionary.OxygenMass; // [Sph-C2H9NO-H]-
                            var acylamide = fattyacidProductIon(acylCarbon, acylDouble) - MassDiffDictionary.OxygenMass + MassDiffDictionary.NitrogenMass + MassDiffDictionary.HydrogenMass + Electron;
                            var query = new List<Peak> {
                                new Peak() { Mz = sphChain_loss, Intensity = 1 },
                                new Peak() { Mz = sphFragment, Intensity = 1 },
                                new Peak() { Mz = acylamide, Intensity = 1 }
                            };

                            //Console.WriteLine("Cer-NP t" + sphCarbon + ":" + sphDouble + "/" + acylCarbon + ":" + acylDouble +
                            //     " " + sphChain_loss + " " + sphFragment + " " + acylamide);

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2)
                            { // the diagnostic acyl ion must be observed for level 2 annotation
                                var molecule = getCeramideMoleculeObjAsLevel2("Cer", LbmClass.Cer_NP, "t", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("Cer-NP", LbmClass.Cer_NP, "t", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}

                    return returnAnnotationResult("Cer", LbmClass.Cer_NP, "t", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
                return null;
        }

        public static LipidMolecule JudgeIfCeramideeos(ObservableCollection<double[]> spectrum, double ms2Tolerance,
        double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
        int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
        int minOmegaacylCarbon, int maxOmegaacylCarbon, int minOmegaacylDoubleBond, int maxOmegaacylDoubleBond,
        AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (maxOmegaacylCarbon > totalCarbon) maxOmegaacylCarbon = totalCarbon;
            if (maxOmegaacylDoubleBond > totalDoubleBond) maxOmegaacylDoubleBond = totalDoubleBond;

            if (adduct.IonMode == IonMode.Negative)
            { // negative ion mode 
                if (adduct.AdductIonName == "[M-H]-" || adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    //var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M-H]-" ? theoreticalMz :
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;

                    // seek [[M-C6H10O5-H]-  // reject HexCer-EOS
                    var threshold1 = 1.0;
                    var diagnosticMz1 = diagnosticMz - 162.052833;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    if (isClassIonFound == true) return null;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz5 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold5 = 50.0;
                        var isClassIon5Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz5, threshold5);
                        if (isClassIon5Found) return null;
                    }

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++)
                        {
                            var remainCarbon = totalCarbon - sphCarbon;
                            var remainDouble = totalDoubleBond - sphDouble;
                            var carbonLimit = Math.Min(remainCarbon, maxOmegaacylCarbon);
                            var doubleLimit = Math.Min(remainDouble, maxOmegaacylDoubleBond);

                            for (int acylCarbon = minOmegaacylCarbon; acylCarbon <= carbonLimit; acylCarbon++)
                            {
                                for (int acylDouble = 0; acylDouble <= doubleLimit; acylDouble++)
                                {
                                    var terminalCarbon = totalCarbon - sphCarbon - acylCarbon;
                                    //if (acylCarbon < maxSphCarbon) break;
                                    var terminalDouble = totalDoubleBond - sphDouble - acylDouble;

                                    var esterloss = diagnosticMz - fattyacidProductIon(terminalCarbon, terminalDouble) + 
                                        MassDiffDictionary.OxygenMass + MassDiffDictionary.HydrogenMass; // 
                                    var esterFa = fattyacidProductIon(terminalCarbon, terminalDouble);
                                    var acylamide = fattyacidProductIon(acylCarbon, acylDouble) + 
                                        MassDiffDictionary.NitrogenMass + MassDiffDictionary.HydrogenMass + Electron;

                                    //Console.WriteLine("d" + sphCarbon + ":" + sphDouble + "/" + omegaAcylCarbon + ":" + omegaAcylDouble + "-O-" +
                                    //    acylCarbon + ":" + acylDouble + " " +
                                    //    esterloss + " " + esterFa + " " + acylamide);

                                    var query1 = new List<Peak> {
                                        new Peak() { Mz = esterFa, Intensity = 30 },
                                    };

                                    var foundCount1 = 0;
                                    var averageIntensity1 = 0.0;
                                    countFragmentExistence(spectrum, query1, ms2Tolerance, out foundCount1, out averageIntensity1);

                                    if (foundCount1 == 1)
                                    { // the diagnostic acyl ion must be observed for level 2 annotation

                                        var query2 = new List<Peak> {
                                                new Peak() { Mz = esterloss, Intensity = 1 },
                                                new Peak() { Mz = esterFa, Intensity = 30 },
                                                new Peak() { Mz = acylamide, Intensity = 0.01 }
                                            };

                                        var foundCount2 = 0;
                                        var averageIntensity2 = 0.0;
                                        countFragmentExistence(spectrum, query2, ms2Tolerance, out foundCount2, out averageIntensity2);

                                        if (foundCount2 == 3) {
                                            var molecule = getEsterceramideMoleculeObjAsLevel2("Cer", LbmClass.Cer_EOS, "d", sphCarbon, sphDouble,
                                               acylCarbon, acylDouble, terminalCarbon, terminalDouble, averageIntensity1);
                                            candidates.Add(molecule);
                                        }
                                        else if (foundCount2 == 2) {
                                            var molecule = getEsterceramideMoleculeObjAsLevel2_0("Cer", LbmClass.Cer_EOS, "d", sphCarbon + acylCarbon,
                                             sphDouble + acylDouble, terminalCarbon, terminalDouble, averageIntensity2);
                                            candidates.Add(molecule);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("Cer-EOS", LbmClass.Cer_EOS, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}
                    if (candidates.Count == 0) return null;
                    // extra esteracyl contains "2O" and 1DoubleBond
                    var extraOxygen = 2;
                    totalDoubleBond = totalDoubleBond + 1;

                    return returnAnnotationResult("Cer", LbmClass.Cer_EOS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, extraOxygen, candidates, 3);
                }
            }
            else if (adduct.IonMode == IonMode.Positive) {
                if (adduct.AdductIonName == "[M+H]+") {
                    // seek -H2O
                    var threshold = 5.0;
                    var diagnosticMz = theoreticalMz - H2O;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    // HexCer exclude
                    var thresholdHex = 30.0;
                    var diagnosticMzHex = diagnosticMz - 162.052833;
                    //var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMzHex, thresholdHex);
                    if (isClassIon2Found) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++) {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) // sphingo chain must have double bond
                        {

                            var acylCarbon = totalCarbon - sphCarbon;
                           // if (acylCarbon < minSphCarbon) { break; }
                            var acylDouble = totalDoubleBond - sphDouble;

                            var sph1 = diagnosticMz - acylCainMass(acylCarbon, acylDouble) + 3 * MassDiffDictionary.HydrogenMass - 2 * MassDiffDictionary.OxygenMass;
                            var sph2 = sph1 - H2O;
                            var sph3 = sph2 - 12; //[Sph-CH4O2+H]+
                            var acylamide = acylCarbon * 12 + (((2 * acylCarbon) - (2 * acylDouble) + 2) * MassDiffDictionary.HydrogenMass) + MassDiffDictionary.OxygenMass + MassDiffDictionary.NitrogenMass;

                            //Console.WriteLine(sphCarbon + ":" + sphDouble + " " + sph1 + " " + sph2 + " " + sph3);

                            var query = new List<Peak> {
                                new Peak() { Mz = sph1, Intensity = 0.01 },
                                new Peak() { Mz = sph2, Intensity = 0.01 },
                                new Peak() { Mz = sph3, Intensity = 0.01 },
                                //new Peak() { Mz = acylamide, Intensity = 0.01 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2) { // 
                                var molecule = getEsterceramideMoleculeObjAsLevel2_1("Cer", LbmClass.Cer_EOS, "d", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (isClassIonFound == false && candidates.Count == 0) return null;
                    // extra esteracyl contains "2O" and 1DoubleBond
                    var extraOxygen = 2;
                    totalDoubleBond = totalDoubleBond + 1;

                    return returnAnnotationResult("Cer", LbmClass.Cer_EOS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, extraOxygen, candidates, 3);

                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfCeramideeods(ObservableCollection<double[]> spectrum, double ms2Tolerance,
        double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
        int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
        int minOmegaacylCarbon, int maxOmegaacylCarbon, int minOmegaacylDoubleBond, int maxOmegaacylDoubleBond,
        AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative) { // negative ion mode 
                if (adduct.AdductIonName == "[M-H]-" || adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-") {
                    // calc [M-H]-
                    //var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M-H]-" ? theoreticalMz :
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz5 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold5 = 50.0;
                        var isClassIon5Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz5, threshold5);
                        if (isClassIon5Found) return null;
                    }

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++) {
                        var sphDouble = 0;

                        var remainCarbon = totalCarbon - sphCarbon;
                        var remainDouble = totalDoubleBond - sphDouble;
                        var carbonLimit = Math.Min(remainCarbon, maxOmegaacylCarbon);
                        var doubleLimit = Math.Min(remainDouble, maxOmegaacylDoubleBond);
                        for (int acylCarbon = minOmegaacylCarbon; acylCarbon <= carbonLimit; acylCarbon++) {
                            for (int acylDouble = 0; acylDouble <= doubleLimit; acylDouble++) {
                                var terminalCarbon = totalCarbon - sphCarbon - acylCarbon;
                                var terminalDouble = totalDoubleBond - sphDouble - acylDouble;

                                var esterloss = diagnosticMz - fattyacidProductIon(terminalCarbon, terminalDouble) 
                                    + MassDiffDictionary.OxygenMass + MassDiffDictionary.HydrogenMass; // 
                                var esterFa = fattyacidProductIon(terminalCarbon, terminalDouble);
                                var acylamide = fattyacidProductIon(acylCarbon, acylDouble) 
                                    + MassDiffDictionary.NitrogenMass + MassDiffDictionary.HydrogenMass + Electron;

                                var query1 = new List<Peak> {
                                        new Peak() { Mz = esterFa, Intensity = 30 },
                                    };

                                var foundCount1 = 0;
                                var averageIntensity1 = 0.0;
                                countFragmentExistence(spectrum, query1, ms2Tolerance, out foundCount1, out averageIntensity1);


                                if (foundCount1 == 1) { // the diagnostic acyl ion must be observed for level 2 annotation

                                    var query2 = new List<Peak> {
                                                new Peak() { Mz = esterloss, Intensity = 1 },
                                                new Peak() { Mz = esterFa, Intensity = 30 },
                                                new Peak() { Mz = acylamide, Intensity = 0.01 }
                                            };

                                    var foundCount2 = 0;
                                    var averageIntensity2 = 0.0;
                                    countFragmentExistence(spectrum, query2, ms2Tolerance, out foundCount2, out averageIntensity2);

                                    if (foundCount2 == 3) {
                                        var molecule = getEsterceramideMoleculeObjAsLevel2("Cer", LbmClass.Cer_EOS, "d", sphCarbon, sphDouble,
                                           acylCarbon, acylDouble, terminalCarbon, terminalDouble, averageIntensity1);
                                        candidates.Add(molecule);
                                    }
                                    else if (foundCount2 == 2) {
                                        var molecule = getEsterceramideMoleculeObjAsLevel2_0("Cer", LbmClass.Cer_EOS, "d", sphCarbon + acylCarbon,
                                         sphDouble + acylDouble, terminalCarbon, terminalDouble, averageIntensity2);
                                        candidates.Add(molecule);
                                    }
                                }
                            }
                        }

                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("Cer-EODS", LbmClass.Cer_EODS, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}
                    if (candidates.Count == 0) return null;
                    // extra esteracyl contains "2O" and 1DoubleBond
                    var extraOxygen = 2;
                    totalDoubleBond = totalDoubleBond + 1;

                    return returnAnnotationResult("Cer", LbmClass.Cer_EODS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, extraOxygen, candidates, 3);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfHexceramideeos(ObservableCollection<double[]> spectrum, double ms2Tolerance,
        double theoreticalMz, int totalCarbon, int totalDoubleBond, // In positive, HexCer-EOS d18:1/34:0: In negative, HexCer-EOS d38:1-O-18:2
        int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
        int minOmegaacylCarbon, int maxOmegaacylCarbon, int minOmegaacylDoubleBond, int maxOmegaacylDoubleBond,
        AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (maxOmegaacylCarbon > totalCarbon) maxOmegaacylCarbon = totalCarbon;
            if (maxOmegaacylDoubleBond > totalDoubleBond) maxOmegaacylDoubleBond = totalDoubleBond;

            if (adduct.IonMode == IonMode.Negative)
            { // negative ion mode 
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    //var threshold = 10.0;
                    var diagnosticMz = 
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                    // seek [[M-C6H10O5-H]-
                    var threshold1 = 1.0;
                    var diagnosticMz1 = diagnosticMz - 162.052833;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    // if (isClassIonFound != true) return null;
                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz5 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold5 = 50.0;
                        var isClassIon5Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz5, threshold5);
                        if (isClassIon5Found) return null;
                    }

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++)
                        {
                            var remainCarbon = totalCarbon - sphCarbon;
                            var remainDouble = totalDoubleBond - sphDouble;
                            var carbonLimit = Math.Min(remainCarbon, maxOmegaacylCarbon);
                            var doubleLimit = Math.Min(remainDouble, maxOmegaacylDoubleBond);

                            for (int omegaAcylCarbon = minOmegaacylCarbon; omegaAcylCarbon <= carbonLimit; omegaAcylCarbon++)
                            {
                                for (int omegaAcylDouble = 0; omegaAcylDouble <= doubleLimit; omegaAcylDouble++)
                                {
                                    var acylCarbon = totalCarbon - sphCarbon - omegaAcylCarbon;
                                    var acylDouble = totalDoubleBond - sphDouble - omegaAcylDouble;

                                    var omegaAcylloss = diagnosticMz - fattyacidProductIon(omegaAcylCarbon, omegaAcylDouble) + MassDiffDictionary.OxygenMass + MassDiffDictionary.HydrogenMass; // 
                                    var omegaAcyllossHexloss = omegaAcylloss - 162.052833; // 
                                    var omegaAcylFA = fattyacidProductIon(omegaAcylCarbon, omegaAcylDouble);

                                    var omegaAcyllossHexH2Oloss = omegaAcyllossHexloss - H2O; // maybe this fragmment is not found
                                    var threshold3 = 1.0;
                                    var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, omegaAcyllossHexH2Oloss, threshold3);
                                    if (isClassIon3Found == true) return null;

                                    var isOmegaAcylFragmentFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, omegaAcylFA, 0.01);
                                    if (!isOmegaAcylFragmentFound) continue;

                                    var query = new List<Peak> {
                                        new Peak() { Mz = omegaAcylloss, Intensity = 0.01 },
                                        new Peak() { Mz = omegaAcyllossHexloss, Intensity = 0.01 },
                                        new Peak() { Mz = omegaAcylFA, Intensity = 0.01 },
                                    };

                                    var foundCount = 0;
                                    var averageIntensity = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                    //if (sphCarbon == 48 && sphDouble == 3 && omegaAcylCarbon == 19 && omegaAcylDouble == 0) {
                                    //    Console.WriteLine();
                                    //}

                                    if (foundCount >= 2)
                                    { // the diagnostic acyl ion must be observed for level 2 annotation
                                        var acylamide = fattyacidProductIon(acylCarbon, acylDouble) + MassDiffDictionary.NitrogenMass + MassDiffDictionary.HydrogenMass + Electron;
                                        var query2 = new List<Peak> {
                                        new Peak() { Mz = acylamide, Intensity = 0.01 }
                                        };
                                        var foundCount2 = 0;
                                        var averageIntensity2 = 0.0;
                                        countFragmentExistence(spectrum, query2, ms2Tolerance, out foundCount2, out averageIntensity2);

                                        if (foundCount2 == 1)
                                        {
                                            var molecule = getEsterceramideMoleculeObjAsLevel2("HexCer", LbmClass.HexCer_EOS, "d", sphCarbon, sphDouble,
                                            acylCarbon, acylDouble, omegaAcylCarbon, omegaAcylDouble, averageIntensity);
                                            candidates.Add(molecule);
                                        }
                                        else
                                        {
                                            var molecule = getEsterceramideMoleculeObjAsLevel2_0("HexCer", LbmClass.HexCer_EOS, "d", sphCarbon + acylCarbon, 
                                                sphDouble+acylDouble, omegaAcylCarbon, omegaAcylDouble, averageIntensity);
                                            candidates.Add(molecule);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("HexCer-EOS", LbmClass.HexCer_EOS, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}
                    if (candidates.Count == 0 && isClassIonFound != true) return null;
                    // extra esteracyl contains "2O" and 1DoubleBond
                    var extraOxygen = 2;
                    totalDoubleBond = totalDoubleBond + 1;

                    return returnAnnotationResult("HexCer", LbmClass.HexCer_EOS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, extraOxygen, candidates, 3);
                }
            }
            else if (adduct.IonMode == IonMode.Positive) {
                if (adduct.AdductIonName == "[M+H]+") {
                    // seek -Hex(-C6H10O5)
                    var threshold = 1.0;
                    var diagnosticMz = theoreticalMz - 162.052833;
                    // seek -H2O -Hex(-C6H10O5)
                    var threshold2 = 1.0;
                    var diagnosticMz2 = diagnosticMz - H2O;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIonFound == !true || isClassIon2Found == !true) return null;

                    //reject HexHexCer


                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++) {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) // sphingo chain must have double bond
                        {

                            var acylCarbon = totalCarbon - sphCarbon;
                            //if (acylCarbon < minSphCarbon) { break; }
                            var acylDouble = totalDoubleBond - sphDouble;

                            var sph1 = diagnosticMz2 - acylCainMass(acylCarbon, acylDouble) + 3 * MassDiffDictionary.HydrogenMass - 2 * MassDiffDictionary.OxygenMass;
                            var sph2 = sph1 - H2O;
                            var sph3 = sph2 - 12; //[Sph-CH4O2+H]+

                            //Console.WriteLine(sphCarbon + ":" + sphDouble + " " + sph1 + " " + sph2 + " " + sph3);

                            var acylamide = acylCarbon * 12 + (((2 * acylCarbon) - (2 * acylDouble) + 2) * MassDiffDictionary.HydrogenMass) + MassDiffDictionary.OxygenMass + MassDiffDictionary.NitrogenMass;

                            var query = new List<Peak> {
                                new Peak() { Mz = sph1, Intensity = 0.01 },
                                new Peak() { Mz = sph2, Intensity = 0.01 },
                                new Peak() { Mz = sph3, Intensity = 0.01 },
                                //new Peak() { Mz = acylamide, Intensity = 0.01 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 1) { // 
                                var molecule = getEsterceramideMoleculeObjAsLevel2_1("HexCer", LbmClass.HexCer_EOS, "d", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    // extra esteracyl contains "2O" and 1DoubleBond
                    var extraOxygen = 2;
                    totalDoubleBond = totalDoubleBond + 1;

                    return returnAnnotationResult("HexCer", LbmClass.HexCer_EOS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, extraOxygen, candidates, 3);
                }
            }
            return null;
        }

        

        public static LipidMolecule JudgeIfCeramideos(ObservableCollection<double[]> spectrum, double ms2Tolerance,
        double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
        int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
        AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            { // negative ion mode 
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    //var threshold = 10.0;
                    var diagnosticMz = 
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                    // seek [M-CH2O-H]-
                    var threshold1 = 0.1;
                    var diagnosticMz1 = diagnosticMz - H2O - 12;
                    // seek [M-CH4O2-H]-
                    var threshold2 = 0.1;
                    var diagnosticMz2 = diagnosticMz1 - H2O;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon1Found != true && isClassIon2Found != true) return null;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz5 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold5 = 50.0;
                        var isClassIon5Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz5, threshold5);
                        if (isClassIon5Found) return null;
                        var diagnosticMz6 = diagnosticMz;
                        var threshold6 = 20.0;
                        var isClassIon6Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz6, threshold6);
                        if (!isClassIon6Found) return null;
                    } else if (adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+FA-H]-") {
                        var diagnosticMz6 = diagnosticMz;
                        var threshold6 = 20.0;
                        var isClassIon6Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz6, threshold6);
                        if (!isClassIon6Found) return null;
                    }


                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++)
                        {
                            var acylCarbon = totalCarbon - sphCarbon;
                            //if (acylCarbon < minSphCarbon) { break; }
                            var acylDouble = totalDoubleBond - sphDouble;

                            var sphChain_loss = diagnosticMz - ((sphCarbon - 2) * 12) - (MassDiffDictionary.HydrogenMass * ((sphCarbon - 2) * 2 - sphDouble * 2 + 1)) -
                                     2 * MassDiffDictionary.OxygenMass - MassDiffDictionary.HydrogenMass; // as [FA+C2H3N]- 
                            var sphFragment = SphingoChainMass(sphCarbon - 2, sphDouble) - MassDiffDictionary.OxygenMass - MassDiffDictionary.NitrogenMass - Proton; // [Sph-NCC-3H]-
                            var acylFragment = fattyacidProductIon(acylCarbon, acylDouble) + MassDiffDictionary.OxygenMass;
                            var query = new List<Peak> {
                                    new Peak() { Mz = sphChain_loss, Intensity = 0.1 },
                                    new Peak() { Mz = sphFragment, Intensity = 1 },
                                    new Peak() { Mz = acylFragment, Intensity = 1 }
                                };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2)
                            { // the diagnostic acyl ion must be observed for level 2 annotation
                                var acylOxidized = 1; //
                                var molecule = getCeramideoxMoleculeObjAsLevel2("Cer", LbmClass.Cer_HS, "d", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, acylOxidized, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
 
                    return returnAnnotationResult("Cer", LbmClass.Cer_HS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 1, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfAcylsm(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
            int minExtAcylCarbon, int maxExtAcylCarbon, int minExtAcylDoubleBond, int maxExtAcylDoubleBond,
            AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (maxExtAcylCarbon > totalCarbon) maxExtAcylCarbon = totalCarbon;
            if (maxExtAcylDoubleBond > totalDoubleBond) maxExtAcylDoubleBond = totalDoubleBond;

            if (adduct.IonMode == IonMode.Negative) { // negative ion mode 
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-") {
                    // seek [M-CH3]-
                    var threshold1 = 50.0;
                    var diagnosticMz1 = adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - 74.036779433 : theoreticalMz - 60.021129369;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    if (isClassIon1Found != true) return null;
                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz2 = theoreticalMz - 60.021129369;
                        var threshold2 = 50.0;
                        var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                        if (isClassIon2Found) return null;
                    }

                    var diagnosticMz3 = 168.0431;
                    var threshold3 = 0.01;
                    var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++) {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) {
                            var remainCarbon = totalCarbon - sphCarbon;
                            var remainDouble = totalDoubleBond - sphDouble;
                            var carbonLimit = Math.Min(remainCarbon, maxSphCarbon);
                            var doubleLimit = Math.Min(remainDouble, maxSphDoubleBond);

                            for (int extCarbon = minExtAcylCarbon; extCarbon <= carbonLimit; extCarbon++) {
                                for (int extDouble = minExtAcylDoubleBond; extDouble <= doubleLimit; extDouble++) {
                                    var acylCarbon = totalCarbon - sphCarbon - extCarbon;
                                    var acylDouble = totalDoubleBond - sphDouble - extDouble;

                                    var extAcylloss = diagnosticMz1 - fattyacidProductIon(extCarbon, extDouble) - MassDiffDictionary.HydrogenMass;  // 
                                    var extFa = fattyacidProductIon(extCarbon, extDouble);

                                    var query = new List<Peak> {
                                        new Peak() { Mz = extAcylloss, Intensity = 0.01 },
                                        new Peak() { Mz = extFa, Intensity = 0.01 },
                                    };

                                    var foundCount = 0;
                                    var averageIntensity = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                    if (foundCount == 2) { // 
                                        var acylamide = fattyacidProductIon(acylCarbon, acylDouble) + MassDiffDictionary.NitrogenMass + MassDiffDictionary.HydrogenMass + Electron + MassDiffDictionary.OxygenMass;
                                        var query2 = new List<Peak>
                                        {
                                        new Peak() { Mz = acylamide, Intensity = 0.01 }
                                        };
                                        var foundCount2 = 0;
                                        var averageIntensity2 = 0.0;
                                        countFragmentExistence(spectrum, query2, ms2Tolerance, out foundCount2, out averageIntensity2);

                                        if (foundCount2 == 1) {
                                            var molecule = getAsmMoleculeObjAsLevel2("SM", LbmClass.ASM, "d", sphCarbon, sphDouble,
                                            acylCarbon, acylDouble, extCarbon, extDouble, averageIntensity);
                                            candidates.Add(molecule);
                                        }
                                        else {
                                            var molecule = getAsmMoleculeObjAsLevel2_0("SM", LbmClass.ASM, "d", sphCarbon + acylCarbon,
                                                sphDouble + acylDouble, extCarbon, extDouble, averageIntensity);
                                            candidates.Add(molecule);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (candidates.Count == 0 && !isClassIon3Found) return null;

                    return returnAnnotationResult("SM", LbmClass.ASM, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 3);
                }
            }
            else if (adduct.IonMode == IonMode.Positive) {
                if (adduct.AdductIonName == "[M+H]+") {
                    var threshold1 = 50.0;
                    var diagnosticMz1 = 184.073;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    if (isClassIon1Found != true) return null;
                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++) {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) {
                            for (int extCarbon = minExtAcylCarbon; extCarbon <= maxExtAcylCarbon; extCarbon++) {
                                for (int extDouble = minExtAcylDoubleBond; extDouble <= maxExtAcylDoubleBond; extDouble++) {
                                    var acylCarbon = totalCarbon - sphCarbon - extCarbon;
                                    var acylDouble = totalDoubleBond - sphDouble - extDouble;

                                    var extAcylloss = theoreticalMz - fattyacidProductIon(extCarbon, extDouble) - MassDiffDictionary.HydrogenMass + Electron;  // 
                                    //Console.WriteLine("ASM {0} Unique mass {1}", "d" + sphCarbon + acylCarbon + ":" + sphDouble + acylDouble + "-O-" + extCarbon + ":" + extDouble, extAcylloss);

                                    var query = new List<Peak> {
                                        new Peak() { Mz = extAcylloss, Intensity = 0.01 },
                                    };

                                    var foundCount = 0;
                                    var averageIntensity = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                    if (foundCount == 1) { // 
                                        var molecule = getAsmMoleculeObjAsLevel2_0("SM", LbmClass.ASM, "d", sphCarbon + acylCarbon,
                                               sphDouble + acylDouble, extCarbon, extDouble, averageIntensity);
                                        candidates.Add(molecule);
                                    }
                                }
                            }
                        }
                    }
                    return returnAnnotationResult("SM", LbmClass.ASM, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 3);
                }
            }
            return null;
        }


        public static LipidMolecule JudgeIfAcylcerbds(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
            int minExtAcylCarbon, int maxExtAcylCarbon, int minExtAcylDoubleBond, int maxExtAcylDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (maxExtAcylCarbon > totalCarbon) maxExtAcylCarbon = totalCarbon;
            if (maxExtAcylDoubleBond > totalDoubleBond) maxExtAcylDoubleBond = totalDoubleBond;

            if (adduct.IonMode == IonMode.Negative)
            { // negative ion mode 
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    var diagnosticMz =
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz5 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold5 = 50.0;
                        var isClassIon5Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz5, threshold5);
                        if (isClassIon5Found) return null;
                    }

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++)
                        {
                            var remainCarbon = totalCarbon - sphCarbon;
                            var remainDouble = totalDoubleBond - sphDouble;
                            var carbonLimit = Math.Min(remainCarbon, maxSphCarbon);
                            var doubleLimit = Math.Min(remainDouble, maxSphDoubleBond);

                            for (int acylCarbon = minExtAcylCarbon; acylCarbon <= carbonLimit; acylCarbon++)
                            {
                                for (int acylDB = minExtAcylDoubleBond; acylDB <= doubleLimit; acylDB++)
                                {
                                    var terminalC = totalCarbon - sphCarbon - acylCarbon;
                                    var terminalDB = totalDoubleBond - sphDouble - acylDB;

                                    var esterloss = diagnosticMz - fattyacidProductIon(terminalC, terminalDB) - MassDiffDictionary.HydrogenMass; // 
                                    var esterFa = fattyacidProductIon(terminalC, terminalDB);
                                    var acylFragment = esterloss - ((sphCarbon - 3) * 12 + ((sphCarbon - 3) * 2 - 2) * MassDiffDictionary.HydrogenMass);

                                    var query = new List<Peak> {
                                        new Peak() { Mz = esterloss, Intensity = 1 },
                                        new Peak() { Mz = esterFa, Intensity = 50 },
                                        new Peak() { Mz = acylFragment, Intensity = 1 }
                                    };

                                    var foundCount = 0;
                                    var averageIntensity = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                    if (foundCount >= 2) { 
                                            var molecule = getEsterceramideMoleculeObjAsLevel2("Cer", LbmClass.Cer_EBDS, "d", sphCarbon, sphDouble,
                                            acylCarbon, acylDB, terminalC, terminalDB, averageIntensity);
                                            candidates.Add(molecule);
                                    }
                                }
                            }
                        }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("AcylCer-BDS", LbmClass.AcylCer_BDS, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}
                    if (candidates.Count == 0) return null;
                    return returnAnnotationResult("Cer", LbmClass.Cer_EBDS, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 3);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfAcylhexceras(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minExtAcylCarbon, int maxExtAcylCarbon, int minExtAcylDoubleBond, int maxExtAcylDoubleBond,
            int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (maxExtAcylCarbon > totalCarbon) maxExtAcylCarbon = totalCarbon;
            if (maxExtAcylDoubleBond > totalDoubleBond) maxExtAcylDoubleBond = totalDoubleBond;

            if (adduct.IonMode == IonMode.Negative)
            { // negative ion mode 
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    var diagnosticMz =
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                    // seek [[M-C6H10O5-H]-  // reject HexCer-EOS
                    var threshold1 = 1.0;
                    var diagnosticMz1 = diagnosticMz - 162.052833;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    //if (isClassIonFound == true) return null;

                    if (adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-") {
                        var diagnosticMz5 = theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                        var threshold5 = 50.0;
                        var isClassIon5Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz5, threshold5);
                        if (isClassIon5Found) return null;
                    }


                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++)
                        {
                            var remainCarbon = totalCarbon - sphCarbon;
                            var remainDouble = totalDoubleBond - sphDouble;
                            //var carbonLimit = Math.Min(remainCarbon, maxExtAcylCarbon);   // use to Brute force calc
                            //var doubleLimit = Math.Min(remainDouble, maxExtAcylDoubleBond); // use to Brute force calc

                            var carbonLimit = maxExtAcylCarbon;
                            var doubleLimit = maxExtAcylDoubleBond;


                            for (int extCarbon = minExtAcylCarbon; extCarbon <= carbonLimit; extCarbon++)
                            {
                                for (int extDouble = minExtAcylDoubleBond; extDouble <= doubleLimit; extDouble++)
                                {
                                    var acylCarbon = totalCarbon - sphCarbon - extCarbon;
                                    var acylDouble = totalDoubleBond - sphDouble - extDouble;

                                    var extAcylLoss = diagnosticMz - fattyacidProductIon(extCarbon, extDouble) - MassDiffDictionary.HydrogenMass + H2O; //[M-FA]-
                                    var extAcylLoss2 = diagnosticMz - fattyacidProductIon(extCarbon, extDouble) - MassDiffDictionary.HydrogenMass;      //[M-FA-H2O]-
                                    var extAcylHexloss = extAcylLoss - 161.04555 - MassDiffDictionary.HydrogenMass;         // 
                                    var extAcylFa = fattyacidProductIon(extCarbon, extDouble);
                                    var sphLoss = diagnosticMz - ((sphCarbon - 2) * 12 + MassDiffDictionary.OxygenMass + MassDiffDictionary.HydrogenMass * ((sphCarbon - 2) * 2) - sphDouble * 2);  //[M-Sph+C2H6NO]-
                                    var sphLoss2 = sphLoss - H2O;      //[M-Sph+C2H4N]-
                                    var query = new List<Peak> {
                                        new Peak() { Mz = extAcylHexloss, Intensity = 1 },
                                        new Peak() { Mz = extAcylFa, Intensity = 1 },
                                        new Peak() { Mz = sphLoss, Intensity = 1 },
                                        new Peak() { Mz = sphLoss2, Intensity = 1 },
                                        new Peak() { Mz = extAcylLoss, Intensity = 10 },
                                        new Peak() { Mz = extAcylLoss2, Intensity = 10 }
                                    };

                                    var foundCount = 0;
                                    var averageIntensity = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                    if (foundCount >= 4)
                                    {
                                        var molecule = getAcylhexceramideMoleculeObjAsLevel2("AHexCer", LbmClass.AHexCer, "d", sphCarbon, sphDouble,
                                        acylCarbon, acylDouble, extCarbon, extDouble, averageIntensity, "+O");
                                        candidates.Add(molecule);
                                    }
                                }
                            }
                        }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("AcylHexCer", LbmClass.AcylHexCer, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}
                    if (candidates.Count == 0) return null;
                    return returnAnnotationResult("AHexCer", LbmClass.AHexCer, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 1, candidates, 3);
                }
            }
            else if (adduct.IonMode == IonMode.Positive) {
                if (adduct.AdductIonName == "[M+H]+") {
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++) {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) {
                            for (int extCarbon = minExtAcylCarbon; extCarbon <= maxExtAcylCarbon; extCarbon++) {
                                for (int extDouble = minExtAcylDoubleBond; extDouble <= maxExtAcylDoubleBond; extDouble++) {
                                    var acylCarbon = totalCarbon - sphCarbon - extCarbon;
                                    var acylDouble = totalDoubleBond - sphDouble - extDouble;
                                   
                                    // eg. AHexCer 16:0/d18:1/22:0h
                                    var exAcylSugarIon = acylCainMass(extCarbon, extDouble) + Sugar162 - Electron; // Hex 16:0, m/z 401
                                   
                                    var ceramideIon = theoreticalMz - acylCainMass(extCarbon, extDouble) - Sugar162 + MassDiffDictionary.HydrogenMass;  // Cer d40:1h, m/z 638.6
                                    var ceramideIon_1WaterLoss = ceramideIon - H2O;
                                    var ceramideIon_2WaterLoss = ceramideIon_1WaterLoss - H2O;

                                    var sphIon = SphingoChainMass(sphCarbon, sphDouble) - MassDiffDictionary.OxygenMass + 2.0 * MassDiffDictionary.HydrogenMass; // Sph d18:1 -H2O, m/z 282;
                                    var sphIon_1H2OLoss = sphIon - H2O; // Sph d18:1 -2H2O, m/z 264;
                                    var sphIon_CH2OLoss = sphIon_1H2OLoss - 12; // Sph d18:1 -CH2O, m/z 252;

                                    //Console.WriteLine("AcylHex {0}, ExAcyl {1}, Cer {2}, Cer-H2O {3}, Cer-2H2O {4}, Sph {5}, Sph-H2O {6}, Sph-CH2O {7}",
                                    //    "AHexCer" + extCarbon + ":" + extDouble + "/d" + sphCarbon + ":" + sphDouble + "/" + acylCarbon + ":" + acylDouble,
                                    //    exAcylSugarIon, ceramideIon, ceramideIon_1WaterLoss, ceramideIon_2WaterLoss, sphIon, sphIon_1H2OLoss, sphIon_CH2OLoss);

                                    var exAcylQuery = new List<Peak>() {
                                        new Peak() { Mz = exAcylSugarIon, Intensity = 1 }
                                    };

                                    var ceramideQuery = new List<Peak>() {
                                        new Peak() { Mz = ceramideIon, Intensity = 1 },
                                        new Peak() { Mz = ceramideIon_1WaterLoss, Intensity = 1 },
                                        new Peak() { Mz = ceramideIon_2WaterLoss, Intensity = 1 }
                                    };

                                    var sphQuery = new List<Peak>() {
                                        new Peak() { Mz = sphIon, Intensity = 1 },
                                        new Peak() { Mz = sphIon_1H2OLoss, Intensity = 1 },
                                        new Peak() { Mz = sphIon_CH2OLoss, Intensity = 1 }
                                    };

                                    var exAcylQueryFoundCount = 0;
                                    var exAcylQueryAverageInt = 0.0;

                                    var ceramideQueryFoundCount = 0;
                                    var ceramideQueryAverageInt = 0.0;

                                    var sphQueryFoundCount = 0;
                                    var sphQueryAverageInt = 0.0;

                                    countFragmentExistence(spectrum, exAcylQuery, ms2Tolerance, out exAcylQueryFoundCount, out exAcylQueryAverageInt);
                                    countFragmentExistence(spectrum, ceramideQuery, ms2Tolerance, out ceramideQueryFoundCount, out ceramideQueryAverageInt);
                                    countFragmentExistence(spectrum, sphQuery, ms2Tolerance, out sphQueryFoundCount, out sphQueryAverageInt);

                                    if (sphQueryFoundCount >= 1 && ceramideQueryFoundCount >= 1 && exAcylQueryFoundCount == 1) {
                                        var molecule = getAcylhexceramideMoleculeObjAsLevel2("AHexCer", LbmClass.AHexCer, "d", sphCarbon, sphDouble,
                                        acylCarbon, acylDouble, extCarbon, extDouble, exAcylQueryAverageInt + ceramideQueryAverageInt + sphQueryAverageInt, "+O");
                                        candidates.Add(molecule);
                                    } else if (ceramideQueryFoundCount >= 1 || exAcylQueryFoundCount == 1) {
                                        var molecule = getAcylhexceramideMoleculeObjAsLevel2_0("AHexCer", LbmClass.AHexCer, "d", sphCarbon + acylCarbon, sphDouble + acylDouble,
                                        extCarbon, extDouble, exAcylQueryAverageInt + ceramideQueryAverageInt, "+O");
                                        candidates.Add(molecule);
                                    } else if (sphQueryFoundCount >= 1) {
                                        var molecule = getAcylhexceramideMoleculeObjAsLevel2_1("AHexCer", LbmClass.AHexCer, "d", sphCarbon, sphDouble,
                                        extCarbon + acylCarbon, extDouble + acylDouble, exAcylQueryAverageInt + ceramideQueryAverageInt, "+O");
                                        candidates.Add(molecule);
                                    }
                                }
                            }
                        }
                    }
                    if (candidates.Count == 0) return null;
                    return returnAnnotationResult("AHexCer", LbmClass.AHexCer, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 1, candidates, 3);
                }
            }
            return null;
        }


        public static LipidMolecule JudgeIfShexcer(ObservableCollection<double[]> spectrum, double ms2Tolerance,
        double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
        int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
        AdductIon adduct, int totalOxidized)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+" || adduct.AdductIonName == "[M+NH4]+")
                {
                    var diagnosticMz = adduct.AdductIonName == "[M+NH4]+" ?
                        theoreticalMz - (MassDiffDictionary.NitrogenMass + MassDiffDictionary.HydrogenMass * 3) : theoreticalMz;
                    // seek [M-SO3-H2O+H]+
                    var threshold = 1.0;
                    var diagnosticMz1 = diagnosticMz - MassDiffDictionary.SulfurMass - 3 * MassDiffDictionary.OxygenMass - H2O - Electron;
                    // seek [M-H2O-SO3-C6H10O5+H]+
                    var threshold2 = 1.0;
                    var diagnosticMz2 = diagnosticMz1 - 162.052833;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon1Found == !true && isClassIon2Found == !true) return null;

                    var hydrogenString = "d";
                    var sphOxidized = 2;
                    var acylOxidized = totalOxidized - sphOxidized;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    if (acylOxidized == 0)
                    {
                        for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                        {
                            for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++)
                            {
                                if (sphCarbon >= 22) continue;
                                var acylCarbon = totalCarbon - sphCarbon;
                                //if (acylCarbon < minSphCarbon) { break; }
                                var acylDouble = totalDoubleBond - sphDouble;

                                var sph1 = diagnosticMz2 - acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.HydrogenMass;
                                var sph2 = sph1 - H2O;
                                var sph3 = sph2 - 12;
                                var acylamide = acylCarbon * 12 + (((2 * acylCarbon) - (2 * acylDouble) + 2) * MassDiffDictionary.HydrogenMass) + MassDiffDictionary.OxygenMass + MassDiffDictionary.NitrogenMass - Electron;


                                var query = new List<Peak> {
                                new Peak() { Mz = sph1, Intensity = 1 },
                                new Peak() { Mz = sph2, Intensity = 1 },
                                new Peak() { Mz = sph3, Intensity = 1 },
                               // new Peak() { Mz = acylamide, Intensity = 0.01 }
                            };

                                var foundCount = 0;
                                var averageIntensity = 0.0;
                                countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                if (foundCount >= 1)
                                { // 
                                    var molecule = getCeramideMoleculeObjAsLevel2("SHexCer", LbmClass.SHexCer, hydrogenString, sphCarbon, sphDouble,
                                        acylCarbon, acylDouble, averageIntensity);
                                    candidates.Add(molecule);
                                }
                            }
                        }
                    }
                    else
                    {   // case of acyl chain have extra OH

                        for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                        {
                            for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++)
                            {
                                var acylCarbon = totalCarbon - sphCarbon;
                                //if (acylCarbon < minSphCarbon) { break; }
                                var acylDouble = totalDoubleBond - sphDouble;

                                var sph1 = diagnosticMz2 - acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.HydrogenMass - MassDiffDictionary.OxygenMass * acylOxidized;
                                var sph2 = sph1 - H2O;
                                var sph3 = sph2 - 12;
                                var acylamide = acylCarbon * 12 + (((2 * acylCarbon) - (2 * acylDouble) + 2) * MassDiffDictionary.HydrogenMass) + MassDiffDictionary.OxygenMass + MassDiffDictionary.NitrogenMass - Electron;
                              
                                //Console.WriteLine("SHexCer {0}:{1};2O/{2}:{3}, sph1={4}, sph2={5}, sph3={6}", sphCarbon, sphDouble, acylCarbon, acylDouble, sph1, sph2, sph3);

                                var query = new List<Peak> {
                                new Peak() { Mz = sph1, Intensity = 1 },
                                new Peak() { Mz = sph2, Intensity = 1 },
                                new Peak() { Mz = sph3, Intensity = 1 },
                               // new Peak() { Mz = acylamide, Intensity = 0.01 }
                            };

                                var foundCount = 0;
                                var averageIntensity = 0.0;
                                countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                if (foundCount >= 1)
                                { // 
                                    var molecule = getCeramideoxMoleculeObjAsLevel2("SHexCer", LbmClass.SHexCer, hydrogenString, sphCarbon, sphDouble,
                                        acylCarbon, acylDouble, acylOxidized, averageIntensity);
                                    candidates.Add(molecule);
                                }
                            }
                        }
                    return returnAnnotationResult("SHexCer", LbmClass.SHexCer, hydrogenString, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, acylOxidized, candidates, 2);

                    }


                }
            }
            else
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek [H2SO4-H]-
                    var threshold = 0.1;
                    var diagnosticMz = 96.960103266;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound != true) return null;

                    // from here, acyl level annotation is executed.
                    //   may be not found fragment to define sphingo and acyl chain
                    var candidates = new List<LipidMolecule>();
                    //var score = 0;
                    //var molecule0 = getCeramideMoleculeObjAsLevel2_0("SHexCer", LbmClass.SHexCer, "d", totalCarbon, totalDoubleBond,
                    //    score);
                    //candidates.Add(molecule0);
                    var hydrogenString = "d";
                    var sphOxidized = 2;
                    var acylOxidized = totalOxidized - sphOxidized;

                    return returnAnnotationResult("SHexCer", LbmClass.SHexCer, hydrogenString, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, acylOxidized, candidates, 2);
                }

            }
            return null;
        }

        public static LipidMolecule JudgeIfGm3(ObservableCollection<double[]> spectrum, double ms2Tolerance,
        double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PC 46:6, totalCarbon = 46 and totalDoubleBond = 6
        int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
        AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+" || adduct.AdductIonName == "[M+NH4]+")
                {
                    // calc [M+H]+
                    var diagnosticMz = adduct.AdductIonName == "[M+NH4]+" ?
                        theoreticalMz - 17.026549 : theoreticalMz;
                    // seek -H2O
                    var threshold1 = 1.0;
                    var diagnosticMz1 = diagnosticMz - H2O;
                    // seek [M-C23H37NO18-H2O+H]+
                    var threshold2 = 1.0;
                    var diagnosticMz2 = diagnosticMz - 12*23 - 19 * H2O - MassDiffDictionary.NitrogenMass - MassDiffDictionary.HydrogenMass;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon1Found == !true || isClassIon2Found == !true) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) // sphingo chain must have double bond
                        {

                            var acylCarbon = totalCarbon - sphCarbon;
                            //if (acylCarbon < minSphCarbon) { break; }
                            var acylDouble = totalDoubleBond - sphDouble;

                            var sph1 = diagnosticMz2 - acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.HydrogenMass;
                            var sph2 = sph1 - H2O;

                            var query = new List<Peak> {
                                new Peak() { Mz = sph1, Intensity = 0.01 },
                                new Peak() { Mz = sph2, Intensity = 0.01 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 1 )
                            { // 
                                var molecule = getCeramideMoleculeObjAsLevel2("GM3", LbmClass.GM3, "d", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //if (candidates == null || candidates.Count == 0)
                    //{
                    //    var score = 0;
                    //    var molecule0 = getCeramideMoleculeObjAsLevel2_0("GM3", LbmClass.GM3, "d", totalCarbon, totalDoubleBond,
                    //        score);
                    //    candidates.Add(molecule0);
                    //}

                    return returnAnnotationResult("GM3", LbmClass.GM3, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek [C11H17NO8-H]-  as 290.0875914768
                    var threshold1 = 0.01;
                    var diagnosticMz1 = 12 * 11 + 8 * H2O + MassDiffDictionary.NitrogenMass;
                    //// seek [M-C11H17NO8-H]-
                    //var threshold2 = 0.01;
                    //var diagnosticMz2 = theoreticalMz - diagnosticMz1 - MassDiffDictionary.HydrogenMass;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    if (isClassIon1Found != true) return null;

                    // from here, acyl level annotation is executed.
                    //   may be not found fragment to define sphingo and acyl chain
                    var candidates = new List<LipidMolecule>();
                        //var score = 0;
                        //var molecule0 = getCeramideMoleculeObjAsLevel2_0("GM3", LbmClass.GM3, "d", totalCarbon, totalDoubleBond,
                        //    score);
                        //candidates.Add(molecule0);

                    return returnAnnotationResult("GM3", LbmClass.GM3, "d", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }

            }
            return null;
        }

        public static LipidMolecule JudgeIfSphinganine(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
    int totalCarbon, int totalDoubleBond, AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {
                    // seek -H2O 
                    var threshold1 = 10.0;
                    var diagnosticMz1 = theoreticalMz - H2O;
                    // seek -2H2O 
                    var threshold2 = 10.0;
                    var diagnosticMz2 = diagnosticMz1 - H2O;
                    // seek -H2O -CH2O
                    var threshold3 = 10.0;
                    var diagnosticMz3 = diagnosticMz2 - 12;
                    // frag -3H2O 
                    var threshold4 = 10.0;
                    var diagnosticMz4 = diagnosticMz2 - H2O;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                    var isClassIon4Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz4, threshold4);

                    var trueCount = 0;
                    if (isClassIon1Found) trueCount++;
                    if (isClassIon2Found) trueCount++;
                    if (isClassIon3Found) trueCount++;
                    //if (isClassIon4Found) trueCount++;

                    //if (isClassIon1Found == !true || isClassIon2Found == !true || isClassIon3Found == !true || isClassIon4Found == true) return null;
                    if (trueCount < 3) return null;
                    var candidates = new List<LipidMolecule>();
                    //var query = new List<Peak> {
                    //            new Peak() { Mz = diagnosticMz1, Intensity = threshold1 },
                    //            new Peak() { Mz = diagnosticMz2, Intensity = threshold2 },
                    //            new Peak() { Mz = diagnosticMz3, Intensity = threshold3 }
                    //        };

                    //var foundCount = 0;
                    //var averageIntensity = 0.0;
                    //countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);
                    //var molecule = getSingleacylchainMoleculeObjAsLevel2("Sphinganine", LbmClass.Sphinganine, totalCarbon, totalDoubleBond,
                    //               averageIntensity);
                    //candidates.Add(molecule);

                    var sphOHCount = 2;

                    return returnAnnotationResult("SPB", LbmClass.DHSph, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, sphOHCount, candidates, 1);
                }

            }
            return null;
        }

        public static LipidMolecule JudgeIfSphingosine(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
            int totalCarbon, int totalDoubleBond, AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {
                    // seek -H2O 
                    var threshold1 = 10.0;
                    var diagnosticMz1 = theoreticalMz - H2O;
                    // seek -2H2O 
                    var threshold2 = 10.0;
                    var diagnosticMz2 = diagnosticMz1 - H2O;
                    // seek -H2O -CH2O
                    var threshold3 = 10.0;
                    var diagnosticMz3 = diagnosticMz2 - 12;
                    // frag -3H2O 
                    var threshold4 = 10.0;
                    var diagnosticMz4 = diagnosticMz2 - H2O;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                    var isClassIon4Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz4, threshold4);
                    //if (isClassIon1Found == !true || isClassIon2Found == !true || isClassIon3Found == !true || isClassIon4Found == true) return null;
                    var trueCount = 0;
                    if (isClassIon1Found) trueCount++;
                    if (isClassIon2Found) trueCount++;
                    if (isClassIon3Found) trueCount++;
                    //if (isClassIon4Found) trueCount++;
                    if (trueCount < 3) return null;

                    var candidates = new List<LipidMolecule>();
                    //var query = new List<Peak> {
                    //            new Peak() { Mz = diagnosticMz1, Intensity = threshold1 },
                    //            new Peak() { Mz = diagnosticMz2, Intensity = threshold2 },
                    //            new Peak() { Mz = diagnosticMz3, Intensity = threshold3 }
                    //        };

                    //var foundCount = 0;
                    //var averageIntensity = 0.0;
                    //countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);
                    //var molecule = getSingleacylchainMoleculeObjAsLevel2("Sphingosine", LbmClass.Sphingosine, totalCarbon, totalDoubleBond,
                    //               averageIntensity);
                    //candidates.Add(molecule);

                    var sphOHCount = 2;

                    return returnAnnotationResult("SPB", LbmClass.Sph, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, sphOHCount, candidates, 1);
                }

            }
            return null;
        }

        public static LipidMolecule JudgeIfPhytosphingosine(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
        int totalCarbon, int totalDoubleBond, AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {
                    // seek -H2O 
                    var threshold1 = 10.0;
                    var diagnosticMz1 = theoreticalMz - H2O;
                    // seek -2H2O 
                    var threshold2 = 10.0;
                    var diagnosticMz2 = diagnosticMz1 - H2O;
                    // seek -3H2O 
                    var threshold3 = 10.0;
                    var diagnosticMz3 = diagnosticMz2 - H2O;
                    // seek -H2O -CH2O
                    var threshold4 = 10.0;
                    var diagnosticMz4 = diagnosticMz2 - 12;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                    var isClassIon4Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz4, threshold4);
                    //if (isClassIon1Found == !true || isClassIon2Found == !true || isClassIon3Found == !true || isClassIon4Found == !true) return null;

                    var trueCount = 0;
                    if (isClassIon1Found) trueCount++;
                    if (isClassIon2Found) trueCount++;
                    if (isClassIon3Found) trueCount++;
                    if (isClassIon4Found) trueCount++;
                    if (trueCount < 3) return null;

                    var candidates = new List<LipidMolecule>();
                    //var query = new List<Peak> {
                    //            new Peak() { Mz = diagnosticMz1, Intensity = threshold1 },
                    //            new Peak() { Mz = diagnosticMz2, Intensity = threshold2 },
                    //            new Peak() { Mz = diagnosticMz3, Intensity = threshold3 },
                    //            new Peak() { Mz = diagnosticMz4, Intensity = threshold4 }
                    //        };

                    //var foundCount = 0;
                    //var averageIntensity = 0.0;
                    //countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);
                    //var molecule = getSingleacylchainMoleculeObjAsLevel2("Phytosphingosine", LbmClass.Phytosphingosine, totalCarbon, totalDoubleBond,
                    //               averageIntensity);
                    //candidates.Add(molecule);

                    var sphOHCount = 3;

                    return returnAnnotationResult("SPB", LbmClass.PhytoSph, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, sphOHCount, candidates, 1);
                }

            }
            return null;
        }


        //add 10/04/19
        public static LipidMolecule JudgeIfEtherpi(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek C6H10O8P-
                    var threshold = 5.0;
                    var diagnosticMz = 241.01188;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound != true) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1_gly = (sn1Carbon + 3) * 12 + (((sn1Carbon + 3) * 2) - (sn1Double * 2) + 1) * MassDiffDictionary.HydrogenMass + 5 * MassDiffDictionary.OxygenMass + MassDiffDictionary.PhosphorusMass - Proton;
                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);

                            var query = new List<Peak> {
                                new Peak() { Mz = sn1_gly, Intensity = 1.0 },
                                new Peak() { Mz = sn2, Intensity = 1.0 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { // now I set 2 as the correct level
                                var molecule = getEtherPhospholipidMoleculeObjAsLevel2("PI", LbmClass.EtherPI, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity, "e");
                                candidates.Add(molecule);
                            }
                        }
                    }

                    return returnAnnotationResult("PI", LbmClass.EtherPI, "e", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfEtherps(ObservableCollection<double[]> spectrum, double ms2Tolerance,
    double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
    int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
    AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek C3H5NO2 loss
                    var threshold = 10.0;
                    var diagnosticMz = theoreticalMz - 87.032029;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound != true) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;
                            if (sn1Carbon >= 24 && sn1Double >= 5) return null;

                            var sn1_gly = (sn1Carbon + 3) * 12 + (((sn1Carbon + 3) * 2) - (sn1Double * 2) + 1) * MassDiffDictionary.HydrogenMass + 5 * MassDiffDictionary.OxygenMass + MassDiffDictionary.PhosphorusMass - Proton;
                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);

                            var query = new List<Peak> {
                                new Peak() { Mz = sn1_gly, Intensity = 30.0 },
                                //new Peak() { Mz = sn2, Intensity = 1.0 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 1)
                            { // now I set 2 as the correct level
                                var molecule = getEtherPhospholipidMoleculeObjAsLevel2("PS", LbmClass.EtherPS, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity, "e");
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (candidates.Count == 0) return null;
                    return returnAnnotationResult("PS", LbmClass.EtherPS, "e", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfPecermide(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
            AdductIon adduct, int totalOxidized)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek [C2H8NO4P-H]-
                    var threshold = 5.0;
                    var diagnosticMz = 140.01182;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIon1Found == false) return null;

                    var hydrogenString = "d";
                    var sphOxidized = 2;
                    var acylOxidized = totalOxidized - sphOxidized; 

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                    {
                        for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++) {

                            var acylCarbon = totalCarbon - sphCarbon;
                            var acylDouble = totalDoubleBond - sphDouble;

                            var acylLoss1 = theoreticalMz - acylCainMass(acylCarbon, acylDouble) + Proton;

                            var query = new List<Peak> {
                                new Peak() { Mz = acylLoss1, Intensity = 0.1 },
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 1) { // now I set 2 as the correct level
                                var molecule = getCeramideMoleculeObjAsLevel2("PE-Cer", LbmClass.PE_Cer, hydrogenString, sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, averageIntensity);
                                candidates.Add(molecule);
                            }
                            var acylLoss2 = theoreticalMz - acylCainMass(acylCarbon, acylDouble) - acylOxidized * MassDiffDictionary.OxygenMass + Proton;
                            var query2 = new List<Peak> {
                                new Peak() { Mz = acylLoss2, Intensity = 0.1 },
                            };

                            var foundCount2 = 0;
                            var averageIntensity2 = 0.0;
                            countFragmentExistence(spectrum, query2, ms2Tolerance, out foundCount2, out averageIntensity2);

                            if (foundCount2 == 1) { // now I set 2 as the correct level
                                var molecule = getCeramideoxMoleculeObjAsLevel2("PE-Cer", LbmClass.PE_Cer, hydrogenString, sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, acylOxidized, averageIntensity2);
                                candidates.Add(molecule);
                            }

                        }
                    }
                    
                    return returnAnnotationResult("PE-Cer", LbmClass.PE_Cer, hydrogenString, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, acylOxidized, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfPicermide(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
            int minSphCarbon, int maxSphCarbon, int minSphDoubleBond, int maxSphDoubleBond,
            AdductIon adduct, int totalOxdyzed)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSphCarbon > totalCarbon) maxSphCarbon = totalCarbon;
            if (maxSphDoubleBond > totalDoubleBond) maxSphDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            {
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek C6H10O8P-
                    var threshold = 5.0;
                    var diagnosticMz = 241.01188;
                    // seek Inositol loss (-C6H10O5)
                    var threshold2 = 1.0;
                    var diagnosticMz2 = theoreticalMz - 162.05282;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon1Found != true || isClassIon2Found != true) return null;

                    var hydrogenString = "d";
                    var sphOxidized = 2;
                    var acylOxidyzed = totalOxdyzed - sphOxidized;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    if (acylOxidyzed == 0)
                    {
                        for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                        {
                            for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++)
                            {
                                var acylCarbon = totalCarbon - sphCarbon;
                                var acylDouble = totalDoubleBond - sphDouble;

                                var acylLoss = theoreticalMz - acylCainMass(acylCarbon, acylDouble) + Proton ;

                                var query = new List<Peak> {
                                    new Peak() { Mz = acylLoss, Intensity = 0.1 }
                                    };

                                var foundCount = 0;
                                var averageIntensity = 0.0;
                                countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);
                                var hydrogenString1 = "d";
                                //if (diagnosticMz - (12 * totalCarbon + (totalCarbon * 2 - totalDoubleBond * 2) * MassDiffDictionary.HydrogenMass + MassDiffDictionary.NitrogenMass + MassDiffDictionary.OxygenMass * 3) > 1)
                                //{
                                //    hydrogenString1 = "t";
                                //}

                                if (foundCount == 1)
                                { // now I set 2 as the correct level
                                    var molecule = getCeramideMoleculeObjAsLevel2("PI-Cer", LbmClass.PI_Cer, hydrogenString1, sphCarbon, sphDouble,
                                        acylCarbon, acylDouble, averageIntensity);
                                    candidates.Add(molecule);
                                }
                            }
                        }

                        return returnAnnotationResult("PI-Cer", LbmClass.PI_Cer, hydrogenString, theoreticalMz, adduct,
                            totalCarbon, totalDoubleBond, acylOxidyzed, candidates, 2);
                    }
                    else
                    { // oxidyzed PI-Cer case

                        for (int sphCarbon = minSphCarbon; sphCarbon <= maxSphCarbon; sphCarbon++)
                        {
                            for (int sphDouble = minSphDoubleBond; sphDouble <= maxSphDoubleBond; sphDouble++)
                            {
                                var acylCarbon = totalCarbon - sphCarbon;
                                var acylDouble = totalDoubleBond - sphDouble;

                                var acylOxidized = totalOxdyzed - sphOxidized;

                                var acylLoss = theoreticalMz - acylCainMass(acylCarbon, acylDouble) - acylOxidized * MassDiffDictionary.OxygenMass + Proton ;

                                var query = new List<Peak> {
                                    new Peak() { Mz = acylLoss, Intensity = 0.1 }
                                    };

                                var foundCount = 0;
                                var averageIntensity = 0.0;
                                countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                if (foundCount == 1)
                                { // now I set 2 as the correct level
                                    var molecule = getCeramideoxMoleculeObjAsLevel2("PI-Cer", LbmClass.PI_Cer, hydrogenString, sphCarbon, sphDouble,
                                        acylCarbon, acylDouble, acylOxidized, averageIntensity);
                                    candidates.Add(molecule);
                                }
                            }
                        }

                        return returnAnnotationResult("PI-Cer", LbmClass.PI_Cer, hydrogenString, theoreticalMz, adduct,
                            totalCarbon, totalDoubleBond, acylOxidyzed, candidates, 2);
                    }

                }
            }
            else if (adduct.AdductIonName == "[M+H]+")
            {
                // seek Header loss (-C6H13O9P)
                var threshold = 1.0;
                var diagnosticMz = theoreticalMz - 260.029722;
                var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                if (isClassIonFound != true) return null;

                //   may be not found fragment to define sphingo and acyl chain
                var candidates = new List<LipidMolecule>();

                var hydrogenString = "d";
                var sphOxidized = 2;
                //if (diagnosticMz - (12 * totalCarbon + (totalCarbon * 2 - totalDoubleBond * 2) * MassDiffDictionary.HydrogenMass + MassDiffDictionary.NitrogenMass + MassDiffDictionary.OxygenMass * 3) > 1)
                //{
                //    hydrogenString = "t";
                //    sphOxidized = 3;
                //}
                var acylOxidyzed = totalOxdyzed - sphOxidized;


                return returnAnnotationResult("PI-Cer", LbmClass.PI_Cer, hydrogenString, theoreticalMz, adduct,
                    totalCarbon, totalDoubleBond, acylOxidyzed, candidates, 2);
            }
            return null;
        }

        public static LipidMolecule JudgeIfDcae(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
            int totalCarbon, int totalDoubleBond, AdductIon adduct, int totalOxidized)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Negative)
            { // Negative ion mode 
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek 373.2748 [M-Acyl-H2O-H]-
                    var threshold1 = 0.1;
                    var diagnosticMz1 = 373.2748;

                    // seek FA-
                    var diagnosticMz2 = fattyacidProductIon(totalCarbon, totalDoubleBond) + totalOxidized * MassDiffDictionary.OxygenMass;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold1);
                    if (isClassIon1Found == false || isClassIon2Found == false) return null;

                    var candidates = new List<LipidMolecule>();

                    return returnAnnotationResult("ST 24:1;O4", LbmClass.DCAE, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, totalOxidized, candidates, 1);
                }

            }
            else if (adduct.AdductIonName == "[M+NH4]+")
            {
                // seek 357.2788063 [M-FA-H2O+H]+
                var threshold3 = 10;
                var diagnosticMz3 = 357.2788063;

                var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                if (isClassIon3Found == false) return null;

                var candidates = new List<LipidMolecule>();

                return returnAnnotationResult("ST 24:1;O4", LbmClass.DCAE, string.Empty, theoreticalMz, adduct,
                    totalCarbon, totalDoubleBond, totalOxidized, candidates, 1);


            }
            return null;
        }

        public static LipidMolecule JudgeIfGdcae(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
            int totalCarbon, int totalDoubleBond, AdductIon adduct, int totalOxidized)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Negative)
            { // Negative ion mode 
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek 430.29628  [M-Acyl-H2O-H]-
                    var threshold = 0.1;
                    var diagnosticMz = 430.29628;

                    // seek FA-
                    var threshold2 = 0.1;
                    var diagnosticMz2 = fattyacidProductIon(totalCarbon, totalDoubleBond) + totalOxidized * MassDiffDictionary.OxygenMass;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIonFound == false || isClassIon2Found == false) return null;

                    var candidates = new List<LipidMolecule>();

                    return returnAnnotationResult("ST 24:1;O3;G", LbmClass.GDCAE, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, totalOxidized, candidates, 1);
                }

            }
            else if (adduct.AdductIonName == "[M+NH4]+")
            {
                // seek 414.30027 [M-FA-H2O+H]+
                var threshold3 = 10;
                var diagnosticMz3 = 414.30027;

                var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                if (isClassIon3Found == false) return null;

                var candidates = new List<LipidMolecule>();

                return returnAnnotationResult("ST 24:1;O3;G", LbmClass.GDCAE, string.Empty, theoreticalMz, adduct,
                    totalCarbon, totalDoubleBond, totalOxidized, candidates, 1);
            }

            return null;
        }
        public static LipidMolecule JudgeIfGlcae(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
    int totalCarbon, int totalDoubleBond, AdductIon adduct, int totalOxidized)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Negative)
            { // Negative ion mode 
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek 414.30137 [M-Acyl-H2O-H]-
                    var threshold = 0.1;
                    var diagnosticMz = 414.30137;

                    // seek FA-
                    var threshold2 = 0.1;
                    var diagnosticMz2 = fattyacidProductIon(totalCarbon, totalDoubleBond) + totalOxidized * MassDiffDictionary.OxygenMass;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIonFound == false || isClassIon2Found == false) return null;

                    var candidates = new List<LipidMolecule>();

                    return returnAnnotationResult("ST 24:1;O2;G", LbmClass.GLCAE, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, totalOxidized, candidates, 1);
                }

            }
            else if (adduct.AdductIonName == "[M+NH4]+")
            {
                // seek 398.3053554 [M-FA+H]+
                var threshold3 = 10;
                var diagnosticMz3 = 416.315920637;

                var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                if (isClassIon3Found == false) return null;

                var candidates = new List<LipidMolecule>();

                return returnAnnotationResult("ST 24:1;O2;G", LbmClass.GLCAE, string.Empty, theoreticalMz, adduct,
                    totalCarbon, totalDoubleBond, totalOxidized, candidates, 1);
            }
            return null;
        }

        public static LipidMolecule JudgeIfTdcae(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
            int totalCarbon, int totalDoubleBond, AdductIon adduct, int totalOxidized)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Negative)
            { // Negative ion mode 
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek 480.27892 [M-Acyl-H2O-H]-
                    var threshold = 0.1;
                    var diagnosticMz = 480.27892;

                    // seek FA-
                    var threshold2 = 0.1;
                    var diagnosticMz2 = fattyacidProductIon(totalCarbon, totalDoubleBond) + totalOxidized * MassDiffDictionary.OxygenMass;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIonFound == false || isClassIon2Found == false) return null;

                    var candidates = new List<LipidMolecule>();

                    return returnAnnotationResult("ST 24:1;O3;T", LbmClass.TDCAE, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, totalOxidized, candidates, 1);
                }
            }
            else if (adduct.AdductIonName == "[M+NH4]+")
            {
                // seek 464.2829058 [M-FA-H2O+H]+
                var threshold3 = 10;
                var diagnosticMz3 = 464.2829058;

                var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                if (isClassIon3Found == false) return null;

                var candidates = new List<LipidMolecule>();

                return returnAnnotationResult("ST 24:1;O3;T", LbmClass.TDCAE, string.Empty, theoreticalMz, adduct,
                    totalCarbon, totalDoubleBond, totalOxidized, candidates, 1);
            }
            return null;
        }

        public static LipidMolecule JudgeIfTlcae(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
            int totalCarbon, int totalDoubleBond, AdductIon adduct, int totalOxidized)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Negative)
            { // Negative ion mode 
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek 464.28400 [M-Acyl-H2O-H]-
                    var threshold = 0.1;
                    var diagnosticMz = 464.28400;

                    // seek FA-
                    var threshold2 = 0.1;
                    var diagnosticMz2 = fattyacidProductIon(totalCarbon, totalDoubleBond) + totalOxidized * MassDiffDictionary.OxygenMass;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIonFound == false || isClassIon2Found == false) return null;

                    var candidates = new List<LipidMolecule>();

                    return returnAnnotationResult("ST 24:1;O2;T", LbmClass.TLCAE, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, totalOxidized, candidates, 1);
                }

            }
            else if (adduct.AdductIonName == "[M+NH4]+")
            {
                // seek 448.2879912 [M-FA+H]+
                var threshold3 = 10;
                var diagnosticMz3 = 466.298556495;

                var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                if (isClassIon3Found == false) return null;

                var candidates = new List<LipidMolecule>();

                return returnAnnotationResult("ST 24:1;O2;T", LbmClass.TLCAE, string.Empty, theoreticalMz, adduct,
                    totalCarbon, totalDoubleBond, totalOxidized, candidates, 1);
            }
            return null;
        }

        public static LipidMolecule JudgeIfLcae(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
        int totalCarbon, int totalDoubleBond, AdductIon adduct, int totalOxidized)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Negative)
            { // Negative ion mode 
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek 357.2799046 [M-Acyl-H2O-H]-
                    var threshold = 0.1;
                    var diagnosticMz = 357.2799046;

                    // seek FA-
                    var threshold2 = 0.1;
                    var diagnosticMz2 = fattyacidProductIon(totalCarbon, totalDoubleBond) + totalOxidized * MassDiffDictionary.OxygenMass;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIonFound == false || isClassIon2Found == false) return null;

                    var candidates = new List<LipidMolecule>();

                    return returnAnnotationResult("ST 24:1;O3", LbmClass.LCAE, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, totalOxidized, candidates, 1);
                }
            }
            else if (adduct.AdductIonName == "[M+NH4]+")
            {
                // seek 359.2944571 [M-FA+H]+
                var threshold3 = 10;
                var diagnosticMz3 = 359.2944571;

                var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                if (isClassIon3Found == false) return null;

                var candidates = new List<LipidMolecule>();

                return returnAnnotationResult("ST 24:1;O3", LbmClass.LCAE, string.Empty, theoreticalMz, adduct,
                    totalCarbon, totalDoubleBond, totalOxidized, candidates, 1);
            }
            return null;
        }

        public static LipidMolecule JudgeIfKlcae(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
        int totalCarbon, int totalDoubleBond, AdductIon adduct, int totalOxidized)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Negative)
            { // Negative ion mode 
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek 371.2591694 [M-Acyl-H2O-H]-
                    var threshold = 0.1;
                    var diagnosticMz = 371.2591694;

                    // seek FA-
                    var threshold2 = 0.1;
                    var diagnosticMz2 = fattyacidProductIon(totalCarbon, totalDoubleBond) + totalOxidized * MassDiffDictionary.OxygenMass;

                    // seek diagnosticMz + H2O (must be not found)
                    var threshold3 = 0.1;
                    var diagnosticMz3 = diagnosticMz + MassDiffDictionary.OxygenMass + 2 * MassDiffDictionary.HydrogenMass;
                    var isClassIonFound3 = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                    if (isClassIonFound3 == true) return null;


                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIonFound == false || isClassIon2Found == false) return null;

                    var candidates = new List<LipidMolecule>();

                    return returnAnnotationResult("ST 24:2;O4", LbmClass.KLCAE, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, totalOxidized, candidates, 1);
                }
            }
            else if (adduct.AdductIonName == "[M+NH4]+")
            {
                // seek 373.2737222 [M-FA+H]+
                var threshold3 = 10;
                var diagnosticMz3 = 373.2737222;

                var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                if (isClassIon3Found == false) return null;

                var candidates = new List<LipidMolecule>();

                return returnAnnotationResult("ST 24:2;O4", LbmClass.KLCAE, string.Empty, theoreticalMz, adduct,
                    totalCarbon, totalDoubleBond, totalOxidized, candidates, 1);
            }
            return null;
        }

        public static LipidMolecule JudgeIfKdcae(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
        int totalCarbon, int totalDoubleBond, AdductIon adduct, int totalOxidized)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Negative)
            { // Negative ion mode 
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek 387.254084 [M-Acyl-H2O-H]-
                    var threshold = 0.1;
                    var diagnosticMz = 387.254084;

                    // seek FA-
                    var threshold2 = 0.1;
                    var diagnosticMz2 = fattyacidProductIon(totalCarbon, totalDoubleBond) + totalOxidized * MassDiffDictionary.OxygenMass;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIonFound == false || isClassIon2Found == false) return null;

                    var candidates = new List<LipidMolecule>();

                    return returnAnnotationResult("ST 24:2;O4", LbmClass.KDCAE, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, totalOxidized, candidates, 1);
                }
            }
            else if (adduct.AdductIonName == "[M+NH4]+")
            {
                // seek 371.2580718 [M-FA-H2O+H]+
                var threshold3 = 10;
                var diagnosticMz3 = 371.2580718;

                var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);
                if (isClassIon3Found == false) return null;

                var candidates = new List<LipidMolecule>();

                return returnAnnotationResult("ST 24:2;O4", LbmClass.KDCAE, string.Empty, theoreticalMz, adduct,
                    totalCarbon, totalDoubleBond, totalOxidized, candidates, 1);
            }
            return null;
        }


        public static LipidMolecule JudgeIfAnandamide(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
            int totalCarbon, int totalDoubleBond, AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive)
            { // Positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {

                    var candidates = new List<LipidMolecule>();

                    return returnAnnotationResult("NAE", LbmClass.NAE, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }

            }
            return null;
        }

        public static LipidMolecule JudgeIfFahfamidegly(ObservableCollection<double[]> spectrum, double ms2Tolerance,
             double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
             int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
             AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+" || adduct.AdductIonName == "[M+NH4]+") {
                    var diagnosticMz = adduct.AdductIonName == "[M+NH4]+" ?
                        theoreticalMz - (MassDiffDictionary.NitrogenMass + MassDiffDictionary.HydrogenMass * 3) : theoreticalMz;
                    var candidates = new List<LipidMolecule>();

                    // seek 76.03930542 (Gly)
                    var threshold2 = 10.0;
                    var diagnosticMz2 = 76.03930542;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    
                    // from here, acyl level annotation is executed.
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;
                            if (sn1Carbon + sn2Carbon < 24) continue;
                            if ((sn1Carbon == 16 && sn1Double == 2) || (sn2Carbon == 16 && sn2Double == 2)) continue;
                                    

                            var sn2Loss = diagnosticMz - fattyacidProductIon(sn1Carbon, sn1Double) - MassDiffDictionary.HydrogenMass;
                            var sn2GlyLoss = sn2Loss - (12 * 2 + MassDiffDictionary.HydrogenMass * 5 + MassDiffDictionary.NitrogenMass + MassDiffDictionary.OxygenMass * 2);
                            var sn2H2OGlyLoss = sn2GlyLoss - H2O;

                            var queryMust = new List<Peak> {
                                    new Peak() { Mz = sn2Loss, Intensity = 5 },
                                };
                            var foundCountMust = 0;
                            var averageIntensitMusty = 0.0;
                            countFragmentExistence(spectrum, queryMust, ms2Tolerance, out foundCountMust, out averageIntensitMusty);
                            if (foundCountMust == 0) continue;
                            var query = new List<Peak> {
                                    //new Peak() { Mz = sn2Loss, Intensity = 5 },
                                    new Peak() { Mz = sn2GlyLoss, Intensity = 5 },
                                    new Peak() { Mz = sn2H2OGlyLoss, Intensity = 5 }
                                };

                            //Console.WriteLine("NAAG " + sn1Carbon + ":" + sn1Double + "/" + sn2Carbon + ":" + sn2Double +
                            //   " " + sn2Loss + " " + sn2GlyLoss + " " + sn2H2OGlyLoss);
                            //if (sn1Carbon == 10 && sn2Carbon == 19) {
                            //    Console.WriteLine();
                            //}

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if ((isClassIonFound && foundCount >= 1) || foundCount == 2) { // now I set 2 as the correct level
                                var molecule = getFahfamideMoleculeObjAsLevel2("NAGly", LbmClass.NAGly, "", sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (isClassIonFound == false && candidates.Count == 0) return null;
                    return returnAnnotationResult("NAGly", LbmClass.NAGly, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else {
                if (adduct.AdductIonName == "[M-H]-") {
                    var candidates = new List<LipidMolecule>();

                    // seek 74.0247525 (Gly)
                    var threshold = 5.0;
                    var diagnosticMz = 74.0247525;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (!isClassIonFound) return null;
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn2Loss = theoreticalMz - fattyacidProductIon(sn1Carbon, sn1Double) - MassDiffDictionary.HydrogenMass;
                            var sn2CO2Loss = sn2Loss - 12 - MassDiffDictionary.OxygenMass * 2;
                            var sn2 = fattyacidProductIon(sn1Carbon, sn1Double);

                            var query = new List<Peak> {
                                    new Peak() { Mz = sn2Loss, Intensity = 10.0 },
                                    new Peak() { Mz = sn2CO2Loss, Intensity = 5.0 },
                                    new Peak() { Mz = sn2, Intensity = 5.0 }
                                };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2) { // now I set 2 as the correct level
                                var molecule = getFahfamideMoleculeObjAsLevel2("NAGly", LbmClass.NAGly, "", sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (isClassIonFound == false && candidates.Count == 0) return null;
                    return returnAnnotationResult("NAGly", LbmClass.NAGly, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfFahfamideglyser(ObservableCollection<double[]> spectrum, double ms2Tolerance,
     double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
     int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
     AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+") {
                    var diagnosticMz = theoreticalMz - (MassDiffDictionary.NitrogenMass + MassDiffDictionary.HydrogenMass * 3);
                    var candidates = new List<LipidMolecule>();

                    // seek 145.06187 (gly+ser-O)
                    var threshold3 = 5.0;
                    var diagnosticMz3 = 145.06187;
                    var isClassIonFound2 = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold3);

                    // from here, acyl level annotation is executed.
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn2Loss = diagnosticMz - fattyacidProductIon(sn1Carbon, sn1Double) - MassDiffDictionary.HydrogenMass;
                            var sn2SerLoss = sn2Loss - (12 * 3 + MassDiffDictionary.HydrogenMass * 7 + MassDiffDictionary.NitrogenMass + MassDiffDictionary.OxygenMass * 3);
                            var sn2SerGlyLoss = sn2SerLoss - (12 * 2 + MassDiffDictionary.HydrogenMass * 3 + MassDiffDictionary.NitrogenMass + MassDiffDictionary.OxygenMass);
                            var ser = (12 * 3 + MassDiffDictionary.HydrogenMass * 8 + MassDiffDictionary.NitrogenMass + MassDiffDictionary.OxygenMass * 3);

                            //Console.WriteLine(sn1Carbon + ":" + sn1Double + "/" + sn1Carbon + ":" + sn1Double +
                            //    " " + sn2Loss + " " + sn2SerLoss + " " + sn2SerGlyLoss + " " + ser);

                            var query = new List<Peak> {
                                        new Peak() { Mz = sn2Loss, Intensity = 5 },
                                        new Peak() { Mz = sn2SerLoss, Intensity = 5 },
                                        new Peak() { Mz = sn2SerGlyLoss, Intensity = 5 },
                                        new Peak() { Mz = ser, Intensity = 5 }
                                    };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 3) {
                                var molecule = getFahfamideMoleculeObjAsLevel2("NAGlySer", LbmClass.NAGlySer, "", sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    if (isClassIonFound2 == false && candidates.Count == 0) return null;
                    return returnAnnotationResult("NAGlySer", LbmClass.NAGlySer, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else {
                if (adduct.AdductIonName == "[M-H]-") {
                    var candidates = new List<LipidMolecule>();
                    //GlySer
                    // seek [M-H]- -H2O
                    var threshold2 = 0.1;
                    var diagnosticMz2 = theoreticalMz - H2O;
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);

                    if (isClassIon2Found == true) {
                        // from here, acyl level annotation is executed.
                        for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++) {
                            for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++) {

                                var sn2Carbon = totalCarbon - sn1Carbon;
                                var sn2Double = totalDoubleBond - sn1Double;

                                var sn2Loss = theoreticalMz - fattyacidProductIon(sn1Carbon, sn1Double) - MassDiffDictionary.HydrogenMass;
                                var sn2CH2OLoss = sn2Loss - 12 - MassDiffDictionary.OxygenMass - MassDiffDictionary.HydrogenMass * 2;
                                var sn2CH2O3Loss = sn2Loss - 12 - MassDiffDictionary.OxygenMass * 3 - MassDiffDictionary.HydrogenMass * 2;
                                var sn2 = fattyacidProductIon(sn1Carbon, sn1Double);

                                var query = new List<Peak> {
                                        new Peak() { Mz = sn2Loss, Intensity = 10.0 },
                                        new Peak() { Mz = sn2CH2OLoss, Intensity = 5.0 },
                                        new Peak() { Mz = sn2CH2O3Loss, Intensity = 5.0 },
                                        new Peak() { Mz = sn2, Intensity = 5.0 }
                                    };

                                var foundCount = 0;
                                var averageIntensity = 0.0;
                                countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                if (foundCount >= 3) {
                                    var molecule = getFahfamideMoleculeObjAsLevel2("NAGlySer", LbmClass.NAGlySer, "", sn1Carbon, sn1Double,
                                        sn2Carbon, sn2Double, averageIntensity);
                                    candidates.Add(molecule);
                                }
                            }
                        }
                        if (candidates.Count == 0) return null;
                        return returnAnnotationResult("NAGlySer", LbmClass.NAGlySer, "", theoreticalMz, adduct,
                            totalCarbon, totalDoubleBond, 0, candidates, 2);

                    }

                }
            }
            return null;
        }



        public static LipidMolecule JudgeIfSulfonolipid(ObservableCollection<double[]> spectrum, double ms2Tolerance,
             double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
             int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
             AdductIon adduct, int totalOxidized) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+" || adduct.AdductIonName == "[M+NH4]+") {
                    var diagnosticMz = adduct.AdductIonName == "[M+NH4]+" ?
                        theoreticalMz - (MassDiffDictionary.NitrogenMass + MassDiffDictionary.HydrogenMass * 3) : theoreticalMz;
                    var candidates = new List<LipidMolecule>();

                    // seek 124.00629  ([C2H6NO3S]+)
                    var threshold2 = 1;
                    var diagnosticMz2 = 124.00629;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIonFound == false) return null;

                    var acylOxidized = totalOxidized - 1;

                    // from here, acyl level annotation is executed.
                    if (acylOxidized == 0) {
                        for (int sphCarbon = minSnCarbon; sphCarbon <= maxSnCarbon; sphCarbon++) {
                            for (int sphDouble = minSnDoubleBond; sphDouble <= maxSnDoubleBond; sphDouble++) {

                                var acylCarbon = totalCarbon - sphCarbon;
                                var acylDouble = totalDoubleBond - sphDouble;

                                var acylLoss = diagnosticMz - acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.HydrogenMass - MassDiffDictionary.OxygenMass * acylOxidized;
                                var acylH2OLoss = acylLoss - H2O;
                                var sph1 = diagnosticMz - (12 * (sphCarbon - 2) + ((sphCarbon - 2) * 2 - 2 * sphDouble - 2) * MassDiffDictionary.HydrogenMass + MassDiffDictionary.OxygenMass);
                                var sph2 = sph1 - H2O;

                                //Console.WriteLine("SL " + sphCarbon + ":" + sphDouble + "/" + acylCarbon + ":" + acylDouble +
                                //   " " + acylLoss + " " + acylH2OLoss + " " + sph1 + " " + sph2);

                                if (acylOxidized == 0) {
                                    var query = new List<Peak> {
                                        new Peak() { Mz = acylLoss, Intensity = 1.0 },
                                        new Peak() { Mz = acylH2OLoss, Intensity = 1.0 },
                                        new Peak() { Mz = sph1, Intensity = 1.0 },
                                        new Peak() { Mz = sph2, Intensity = 1.0 }
                                };

                                    var foundCount = 0;
                                    var averageIntensity = 0.0;
                                    countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                                    if (foundCount >= 3) {
                                        var molecule = getCeramideoxMoleculeObjAsLevel2("SL", LbmClass.SL, "m", sphCarbon, sphDouble,
                                            acylCarbon, acylDouble, acylOxidized, averageIntensity);
                                        candidates.Add(molecule);
                                    }
                                }
                            }
                        }
                    }
                    else {
                        for (int sphCarbon = minSnCarbon; sphCarbon <= maxSnCarbon; sphCarbon++) {
                            for (int sphDouble = minSnDoubleBond; sphDouble <= maxSnDoubleBond; sphDouble++) {

                                var acylCarbon = totalCarbon - sphCarbon;
                                var acylDouble = totalDoubleBond - sphDouble;

                                var acylLoss = diagnosticMz - acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.HydrogenMass - MassDiffDictionary.OxygenMass * acylOxidized;
                                var acylH2OLoss = acylLoss - H2O;
                                var sph3 = diagnosticMz - (12 * (sphCarbon - 2 + 1) + ((sphCarbon - 2) * 2 - 2 * sphDouble - 2) * MassDiffDictionary.HydrogenMass + MassDiffDictionary.OxygenMass * 2);
                                var acylCain = acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.OxygenMass * acylOxidized;


                                var query2 = new List<Peak> {
                                new Peak() { Mz = acylH2OLoss, Intensity = 1.0 },
                                new Peak() { Mz = sph3, Intensity = 1.0 },
                                };

                                var foundCount2 = 0;
                                var averageIntensity = 0.0;
                                countFragmentExistence(spectrum, query2, ms2Tolerance, out foundCount2, out averageIntensity);

                                //Console.WriteLine("SL+O " + sphCarbon + ":" + sphDouble + "/" + acylCarbon + ":" + acylDouble +
                                //    " " + acylH2OLoss + " " + sph3);

                                if (foundCount2 >= 2) {
                                    var molecule = getCeramideoxMoleculeObjAsLevel2("SL", LbmClass.SL, "m", sphCarbon, sphDouble,
                                        acylCarbon, acylDouble, acylOxidized, averageIntensity);
                                    candidates.Add(molecule);
                                }

                            }
                        }
                    }
                    if (isClassIonFound == false && candidates.Count == 0) return null;
                    return returnAnnotationResult("SL", LbmClass.SL, "m", theoreticalMz, adduct,
                    totalCarbon, totalDoubleBond, acylOxidized, candidates, 2);
                }
            }
            else {
                if (adduct.AdductIonName == "[M-H]-") {
                    var candidates = new List<LipidMolecule>();
                    // seek SO3- or HSO3-
                    var threshold = 0.1;
                    var diagnosticMz1 = 79.95736;
                    var diagnosticMz2 = 80.96409;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);

                    if (isClassIon1Found == !true && isClassIon2Found == !true) return null;
                    // from here, acyl level annotation is executed.

                    var acylOxidized = totalOxidized - 1;

                    for (int sphCarbon = minSnCarbon; sphCarbon <= maxSnCarbon; sphCarbon++) {
                        for (int sphDouble = minSnDoubleBond; sphDouble <= maxSnDoubleBond; sphDouble++) {

                            var acylCarbon = totalCarbon - sphCarbon;
                            var acylDouble = totalDoubleBond - sphDouble;

                            var acylLoss = theoreticalMz - acylCainMass(acylCarbon, acylDouble) + MassDiffDictionary.HydrogenMass - MassDiffDictionary.OxygenMass * acylOxidized;

                            var query = new List<Peak> {
                                new Peak() { Mz = acylLoss, Intensity = 1.0 },
                                };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 1) { // now I set 2 as the correct level
                                var molecule = getCeramideoxMoleculeObjAsLevel2("SL", LbmClass.SL, "m", sphCarbon, sphDouble,
                                    acylCarbon, acylDouble, acylOxidized, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    return returnAnnotationResult("SL", LbmClass.SL, "m", theoreticalMz, adduct,
                    totalCarbon, totalDoubleBond, acylOxidized, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfEtherpg(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PS 46:6, totalCarbon = 46 and totalDoubleBond = 6
           int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
           AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            { // Negative ion mode 
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek C3H7O5P-
                    var threshold = 0.01;
                    var diagnosticMz = 152.995833871;
                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);

                    var threshold2 = 30;
                    var diagnosticMz2 = theoreticalMz - 63.008491; // [M+C2H3N(ACN)+Na-2H]- adduct of PE [M-H]- 
                    var isClassIonFound2 = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIonFound2) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = sn1Carbon * 12 + (sn1Carbon * 2 + 1 - sn1Double * 2) * MassDiffDictionary.HydrogenMass + MassDiffDictionary.OxygenMass*2 +Electron ; // (maybe) ether chain rearrange
                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);

                            var query = new List<Peak> {
                                new Peak() { Mz = sn1, Intensity = 5.0 },
                                new Peak() { Mz = sn2, Intensity = 10.0 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 2)
                            { // now I set 2 as the correct level
                                var molecule = getEtherPhospholipidMoleculeObjAsLevel2("PG", LbmClass.EtherPG, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity, "e");
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //if (isClassIonFound == false && candidates.Count == 0) return null;
                    if (candidates.Count == 0) return null;

                    return returnAnnotationResult("PG", LbmClass.EtherPG, "e", theoreticalMz, adduct,
                      totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfEtherlysopg(ObservableCollection<double[]> spectrum, double ms2Tolerance,
    double theoreticalMz, int totalCarbon, int totalDoubleBond, // 
    int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
    AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            { // negative ion mode 
                if (adduct.AdductIonName == "[M-H]-")
                {
                    var diagnosticMz1 = 152.99583;  // seek C3H6O5P-
                    var threshold1 = 10.0;
                    var diagnosticMz2 = totalCarbon * 12 + (2 * (totalCarbon - totalDoubleBond )+ 1) * MassDiffDictionary.HydrogenMass + MassDiffDictionary.OxygenMass; // seek [Ether fragment]-
                    var threshold2 = 1.0;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if (isClassIon1Found != true && isClassIon2Found != true) return null;

                    //
                    var candidates = new List<LipidMolecule>();
                    return returnAnnotationResult("LPG", LbmClass.EtherLPG, "e", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfCoenzymeq(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double theoreticalMz, int totalCarbon, int totalDoubleBond, // 
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {
                    var diagnosticMz1 = 197.0808164;  // seek [(C9H9O4)+CH3+H]+
                    var threshold1 = 10.0;
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    if (isClassIon1Found != true ) return null;

                    //
                    var candidates = new List<LipidMolecule>();
                    var coqSurfix = Math.Round((theoreticalMz - 182.057908802) / (12 * 5 + MassDiffDictionary.HydrogenMass * 8));

                    return returnAnnotationNoChainResult("CoQ"+ coqSurfix, LbmClass.CoQ, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfVitaminmolecules(ObservableCollection<double[]> spectrum, double ms2Tolerance,
                double theoreticalMz, int totalCarbon, int totalDoubleBond, // 
                AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+" || adduct.AdductIonName == "[M+Na]+")
                {
                    // calc [M+H]+
                    var diagnosticMz = adduct.AdductIonName == "[M+H]+" ? theoreticalMz : theoreticalMz - 22.9892207;
                    // vitamin D
                    var vitamindMz = 401.3414071;
                    var threshold = 0.01;

                    var isVitamindFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, vitamindMz, threshold);
                    if (isVitamindFound == true)
                    {
                        var threshold1 = 10.0;
                        var diagnosticMz1 = diagnosticMz - H2O;
                        var diagnosticMz2 = diagnosticMz1 - H2O;

                        var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                        var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold1);
                        if (isClassIon1Found != true && isClassIon1Found != true) return null;

                        //
                        var candidates = new List<LipidMolecule>();
                        return returnAnnotationNoChainResult("25-hydroxycholecalciferol", LbmClass.Vitamin_E, "", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 0);
                    }
                }
                return null;
            }
            else
            {
                if (adduct.AdductIonName == "[M-H]-" || adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    //var threshold = 10.0;
                    var diagnosticMz = adduct.AdductIonName == "[M-H]-" ? theoreticalMz :
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                    // vitamin E
                    var vitamineMz = 429.3738044;
                    var threshold = 1;

                    var isVitamindFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, vitamineMz, threshold);
                    if (isVitamindFound == true)
                    {
                        var threshold1 = 10.0;
                        var diagnosticMz1 = 163.0753564; //[C10H11O2]-
                        var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                        if (isClassIon1Found != true) return null;

                        //
                        var candidates = new List<LipidMolecule>();
                        return returnAnnotationNoChainResult("alpha-Tocopherol", LbmClass.Vitamin_E, "", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 0);
                    }
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfSterolHexoside(string lipidname, LbmClass lipidclass, ObservableCollection<double[]> spectrum, double ms2Tolerance,
               double theoreticalMz, int totalCarbon, int totalDoubleBond, AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+") {
                    // calc [M+H]+
                    var diagnosticMz = theoreticalMz - 179.0561136;
                    var threshold = 0.01;

                    var isSterolFrag = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isSterolFrag == true) {
                        var candidates = new List<LipidMolecule>();
                        return returnAnnotationNoChainResult(lipidname, lipidclass, "", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 0);
                    }
                }
                return null;
            }
            else {
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-") {
                    // calc [M-H]-
                    //var threshold = 10.0;
                    var diagnosticMz = 
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;
                    var diagnosticCutoff = 1.0;
                    // hexose
                    var hexoseMz = 179.0561136;
                    var threshold = 0.01;

                    var isHexoseFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, hexoseMz, threshold);
                    var isDiagnosticFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, diagnosticCutoff);
                    if (isHexoseFound && isDiagnosticFound) {
                        var candidates = new List<LipidMolecule>();
                        return returnAnnotationNoChainResult(lipidname, lipidclass, "", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 0);
                    }
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfSterolSulfate(string lipidname, LbmClass lipidclass, ObservableCollection<double[]> spectrum, double ms2Tolerance,
               double theoreticalMz, int totalCarbon, int totalDoubleBond, AdductIon adduct) {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive) { // positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+") {
                    // calc [M+H]+
                    var diagnosticMz = theoreticalMz - 96.960103266;
                    var threshold = 0.01;

                    var isSterolFrag = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isSterolFrag == true) {
                        var candidates = new List<LipidMolecule>();
                        return returnAnnotationNoChainResult(lipidname, lipidclass, "", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 0);
                    }
                }
                return null;
            }
            else {
                if (adduct.AdductIonName == "[M-H]-") {
                    // sulfate
                    var hexoseMz = 96.960103266;
                    var threshold = 0.01;

                    var isSulfateFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, hexoseMz, threshold);
                    if (isSulfateFound) {
                        var candidates = new List<LipidMolecule>();
                        return returnAnnotationNoChainResult(lipidname, lipidclass, "", theoreticalMz, adduct,
                           totalCarbon, totalDoubleBond, 0, candidates, 0);
                    }
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfVitaminaestermolecules(ObservableCollection<double[]> spectrum, double ms2Tolerance,
    double theoreticalMz, int totalCarbon, int totalDoubleBond, AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+" || adduct.AdductIonName == "[M+Na]+")
                {
                    // calc [M+H]+
                    var diagnosticMz = adduct.AdductIonName == "[M+H]+" ? theoreticalMz : theoreticalMz - 22.9892207;
                    // retinyl ester
                    var threshold1 = 1.0;
                    var diagnosticMz1 = 269.2263771; // SN1 loss
                    var diagnosticMz2 = 169.1011771; // SN1 and C7H16 loss
                    var diagnosticMz3 = 119.0855264; // SN1 and C7H16 loss

                    if (adduct.AdductIonName == "[M+H]+") {
                        var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                        var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold1);
                        if (isClassIon1Found != true && isClassIon2Found != true) return null;
                    } else if (adduct.AdductIonName == "[M+Na]+") {
                        var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                        var isClassIon3Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz3, threshold1);
                        if (isClassIon1Found != true && isClassIon3Found != true) return null;
                    }

                    //
                    var candidates = new List<LipidMolecule>();
                    return returnAnnotationResult("VAE", LbmClass.VAE, "", theoreticalMz, adduct,
                       totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfFahfamideorn(ObservableCollection<double[]> spectrum, double ms2Tolerance,
     double theoreticalMz, int totalCarbon, int totalDoubleBond, // If the candidate PE 46:6, totalCarbon = 46 and totalDoubleBond = 6
     int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
     AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Positive)
            { // positive ion mode 
                if (adduct.AdductIonName == "[M+H]+")
                {
                    var candidates = new List<LipidMolecule>();

                    var threshold = 1.0;
                    var diagnosticMz1 = 115.0865894; //[C5H10N2O+H]+  fragment of Orn
                    var diagnosticMz2 = 70.06512542; //[C4H7N+H]+     fragment of Orn
                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold);
                    if ((isClassIon1Found == false || isClassIon2Found == false)) return null;


                    // from here, acyl level annotation is executed.
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {

                        var sn2Carbon = totalCarbon - sn1Carbon;
                        var sn2Double = totalDoubleBond - sn1Double;

                        var sn2Loss = theoreticalMz - fattyacidProductIon(sn1Carbon, sn1Double) - MassDiffDictionary.HydrogenMass;
                            var sn2H2OLoss = sn2Loss - H2O;
                            var sn1Fragment = sn1Carbon * 12 + ((sn1Carbon - (sn1Double + 1)) * 2 - 2) * MassDiffDictionary.HydrogenMass + MassDiffDictionary.OxygenMass + Proton;

                            var query = new List<Peak> {
                                new Peak() { Mz = sn2Loss, Intensity = 5 },
                                new Peak() { Mz = sn2H2OLoss, Intensity = 5 },
                                new Peak() { Mz = sn1Fragment, Intensity = 5 }
                            };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2)
                            { // now I set 2 as the correct level
                                var molecule = getFahfamideMoleculeObjAsLevel2("NAOrn", LbmClass.NAOrn, "", sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    //if ((isClassIon1Found == !true || isClassIon2Found == !true) && candidates.Count == 0) return null;
                    return returnAnnotationResult("NAOrn", LbmClass.NAOrn, "", theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfBrseSpecies(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
        int totalCarbon, int totalDoubleBond, AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive)
            { // Positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // seek 381.35158  sterol structure (Brassica-sterol)
                    var threshold = 10;
                    var diagnosticMz = 381.35158;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    var candidates = new List<LipidMolecule>();
                    var steroidalModificationClass = "ester";
                    var molecule = getSteroidalEtherMoleculeObj("SE", LbmClass.BRSE, "28:2", steroidalModificationClass, totalCarbon, totalDoubleBond);
                    candidates.Add(molecule);

                    return returnAnnotationResult("SE", LbmClass.BRSE, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfCaseSpecies(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
        int totalCarbon, int totalDoubleBond, AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive)
            { // Positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // seek 383.36723  sterol structure (Campe-sterol)
                    var threshold = 60;
                    var diagnosticMz = 383.36723;
                    if (totalCarbon == 18 && totalDoubleBond == 5) return null;
                    if (totalCarbon == 19 && totalDoubleBond == 5) return null;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    var candidates = new List<LipidMolecule>();
                    var steroidalModificationClass = "ester";
                    var molecule = getSteroidalEtherMoleculeObj("SE", LbmClass.CASE, "28:1", steroidalModificationClass, totalCarbon, totalDoubleBond);
                    candidates.Add(molecule);

                    return returnAnnotationResult("SE", LbmClass.CASE, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfSiseSpecies(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
        int totalCarbon, int totalDoubleBond, AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive)
            { // Positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // seek 397.38288  sterol structure (Sito-sterol)
                    var threshold = 10;
                    var diagnosticMz = 397.38288;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    var candidates = new List<LipidMolecule>();
                    var steroidalModificationClass = "ester";
                    var molecule = getSteroidalEtherMoleculeObj("SE", LbmClass.SISE, "29:1", steroidalModificationClass, totalCarbon, totalDoubleBond);
                    candidates.Add(molecule);

                    return returnAnnotationResult("SE", LbmClass.SISE, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfStseSpecies(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
        int totalCarbon, int totalDoubleBond, AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive)
            { // Positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // seek 395.36723  sterol structure (Stigma-sterol)
                    var threshold = 10;
                    var diagnosticMz = 395.36723;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    var candidates = new List<LipidMolecule>();
                    var steroidalModificationClass = "ester";
                    var molecule = getSteroidalEtherMoleculeObj("SE", LbmClass.STSE, "29:2", steroidalModificationClass, totalCarbon, totalDoubleBond);
                    candidates.Add(molecule);

                    return returnAnnotationResult("SE", LbmClass.STSE, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfAhexbrseSpecies(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
        int totalCarbon, int totalDoubleBond, AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive)
            { // Positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // seek 381.35158  sterol structure (Brassica-sterol)
                    var threshold = 10;
                    var diagnosticMz = 381.35158;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    var candidates = new List<LipidMolecule>();
                    var steroidalModificationClass = "AHex";
                    var molecule = getSteroidalEtherMoleculeObj("ST", LbmClass.AHexBRS, "28:2", steroidalModificationClass, totalCarbon, totalDoubleBond);
                    candidates.Add(molecule);

                    return returnAnnotationResult("ST", LbmClass.AHexBRS, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    var diagnosticMz = 
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;

                    // seek FA-
                    var threshold1 = 5.0;
                    var diagnosticMz1 = fattyacidProductIon(totalCarbon, totalDoubleBond);

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    if (isClassIon1Found == false) return null;

                    var candidates = new List<LipidMolecule>();
                    var steroidalModificationClass = "AHex";
                    var molecule = getSteroidalEtherMoleculeObj("ST", LbmClass.AHexBRS, "28:2", steroidalModificationClass, totalCarbon, totalDoubleBond);
                    candidates.Add(molecule);

                    return returnAnnotationResult("ST", LbmClass.AHexBRS, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfAhexcaseSpecies(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
        int totalCarbon, int totalDoubleBond, AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive)
            { // Positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // seek 383.36723  sterol structure (Campe-sterol)
                    var threshold = 10;
                    var diagnosticMz = 383.36723;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    var candidates = new List<LipidMolecule>();
                    var steroidalModificationClass = "AHex";
                    var molecule = getSteroidalEtherMoleculeObj("ST", LbmClass.AHexCAS, "28:1", steroidalModificationClass, totalCarbon, totalDoubleBond);
                    candidates.Add(molecule);

                    return returnAnnotationResult("ST", LbmClass.AHexCAS, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    var diagnosticMz =
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;

                    // seek FA-
                    var threshold1 = 5.0;
                    var diagnosticMz1 = fattyacidProductIon(totalCarbon, totalDoubleBond);

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    if (isClassIon1Found == false) return null;

                    var candidates = new List<LipidMolecule>();
                    var steroidalModificationClass = "AHex";
                    var molecule = getSteroidalEtherMoleculeObj("ST", LbmClass.AHexCAS, "28:1", steroidalModificationClass, totalCarbon, totalDoubleBond);
                    candidates.Add(molecule);

                    return returnAnnotationResult("ST", LbmClass.AHexCAS, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfAhexceSpecies(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
        int totalCarbon, int totalDoubleBond, AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive)
            { // Positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // seek 369.35158  sterol structure (Chole-sterol)
                    var threshold = 10;
                    var diagnosticMz = 369.35158;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    var candidates = new List<LipidMolecule>();
                    var steroidalModificationClass = "AHex";
                    var molecule = getSteroidalEtherMoleculeObj("ST", LbmClass.AHexCS, "27:1", steroidalModificationClass, totalCarbon, totalDoubleBond);
                    candidates.Add(molecule);

                    return returnAnnotationResult("ST", LbmClass.AHexCS, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    var diagnosticMz =
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;

                    // seek FA-
                    var threshold1 = 5.0;
                    var diagnosticMz1 = fattyacidProductIon(totalCarbon, totalDoubleBond);

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    if (isClassIon1Found == false) return null;

                    var candidates = new List<LipidMolecule>();
                    var steroidalModificationClass = "AHex";
                    var molecule = getSteroidalEtherMoleculeObj("ST", LbmClass.AHexCS, "27:1", steroidalModificationClass, totalCarbon, totalDoubleBond);
                    candidates.Add(molecule);

                    return returnAnnotationResult("ST", LbmClass.AHexCS, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfAhexsiseSpecies(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
        int totalCarbon, int totalDoubleBond, AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive)
            { // Positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // seek 397.38288  sterol structure (Sito-sterol)
                    var threshold = 10;
                    var diagnosticMz = 397.38288;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    var candidates = new List<LipidMolecule>();
                    var steroidalModificationClass = "AHex";
                    var molecule = getSteroidalEtherMoleculeObj("ST", LbmClass.AHexSIS, "29:1", steroidalModificationClass, totalCarbon, totalDoubleBond);
                    candidates.Add(molecule);

                    return returnAnnotationResult("ST", LbmClass.AHexSIS, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    var diagnosticMz =
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;

                    // seek FA-
                    var threshold1 = 5.0;
                    var diagnosticMz1 = fattyacidProductIon(totalCarbon, totalDoubleBond);

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    if (isClassIon1Found == false) return null;

                    var candidates = new List<LipidMolecule>();
                    var steroidalModificationClass = "AHex";
                    var molecule = getSteroidalEtherMoleculeObj("ST", LbmClass.AHexSIS, "29:1", steroidalModificationClass, totalCarbon, totalDoubleBond);
                    candidates.Add(molecule);

                    return returnAnnotationResult("ST", LbmClass.AHexSIS, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfAhexstseSpecies(ObservableCollection<double[]> spectrum, double ms2Tolerance, float theoreticalMz,
        int totalCarbon, int totalDoubleBond, AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (adduct.IonMode == IonMode.Positive)
            { // Positive ion mode 
                if (adduct.AdductIonName == "[M+NH4]+")
                {
                    // seek 395.36723  sterol structure (Stigma-sterol)
                    var threshold = 10;
                    var diagnosticMz = 395.36723;

                    var isClassIonFound = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz, threshold);
                    if (isClassIonFound == false) return null;

                    var candidates = new List<LipidMolecule>();
                    var steroidalModificationClass = "AHex";
                    var molecule = getSteroidalEtherMoleculeObj("ST", LbmClass.AHexSTS, "29:2", steroidalModificationClass, totalCarbon, totalDoubleBond);
                    candidates.Add(molecule);

                    return returnAnnotationResult("ST", LbmClass.AHexSTS, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            else
            {
                if (adduct.AdductIonName == "[M+FA-H]-" || adduct.AdductIonName == "[M+Hac-H]-" ||
                    adduct.AdductIonName == "[M+HCOO]-" || adduct.AdductIonName == "[M+CH3COO]-")
                {
                    // calc [M-H]-
                    var diagnosticMz =
                        adduct.AdductIonName == "[M+CH3COO]-" || adduct.AdductIonName == "[M+Hac-H]-" ?
                        theoreticalMz - MassDiffDictionary.HydrogenMass - 59.013864 : theoreticalMz - MassDiffDictionary.HydrogenMass - 44.998214;

                    // seek FA-
                    var threshold1 = 5.0;
                    var diagnosticMz1 = fattyacidProductIon(totalCarbon, totalDoubleBond);

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    if (isClassIon1Found == false) return null;

                    var candidates = new List<LipidMolecule>();
                    var steroidalModificationClass = "AHex";
                    var molecule = getSteroidalEtherMoleculeObj("ST", LbmClass.AHexSTS, "29:2", steroidalModificationClass, totalCarbon, totalDoubleBond);
                    candidates.Add(molecule);

                    return returnAnnotationResult("ST", LbmClass.AHexSTS, string.Empty, theoreticalMz, adduct,
                        totalCarbon, totalDoubleBond, 0, candidates, 1);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfSmgdg(ObservableCollection<double[]> spectrum, double ms2Tolerance,
           double theoreticalMz, int totalCarbon, int totalDoubleBond, 
            int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
            AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            { // negative ion mode 
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek 225.0069 [C6H9O8S]-
                    var threshold1 = 0.1;
                    var diagnosticMz1 = 241.0024;
                    // seek 225.0069 [H2SO4-H]-
                    var threshold2 = 0.1;
                    var diagnosticMz2 = 96.9601;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if ((isClassIon1Found == false || isClassIon2Found == false)) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            var sn1 = fattyacidProductIon(sn1Carbon, sn1Double);
                            var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);
                            var nl_SN1 = theoreticalMz - acylCainMass(sn1Carbon, sn1Double) - H2O + Proton;
                            var nl_SN2 = theoreticalMz - acylCainMass(sn2Carbon, sn2Double) - H2O + Proton;

                            var query = new List<Peak> {
                                new Peak() { Mz = sn1, Intensity = 0.1 },
                                new Peak() { Mz = sn2, Intensity = 0.1 },
                                new Peak() { Mz = nl_SN1, Intensity = 0.1 },
                                new Peak() { Mz = nl_SN2, Intensity = 0.1 }
                            };
                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount >= 2)
                            { // now I set 2 as the correct level
                                var molecule = getPhospholipidMoleculeObjAsLevel2("SMGDG", LbmClass.SMGDG, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity);
                                candidates.Add(molecule);
                            }
                        }
                    }
                    return returnAnnotationResult("SMGDG", LbmClass.SMGDG, "", theoreticalMz, adduct,
                      totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            return null;
        }

        public static LipidMolecule JudgeIfEtherSmgdg(ObservableCollection<double[]> spectrum, double ms2Tolerance,
   double theoreticalMz, int totalCarbon, int totalDoubleBond,
    int minSnCarbon, int maxSnCarbon, int minSnDoubleBond, int maxSnDoubleBond,
    AdductIon adduct)
        {
            if (spectrum == null || spectrum.Count == 0) return null;
            if (maxSnCarbon > totalCarbon) maxSnCarbon = totalCarbon;
            if (maxSnDoubleBond > totalDoubleBond) maxSnDoubleBond = totalDoubleBond;
            if (adduct.IonMode == IonMode.Negative)
            { // negative ion mode 
                if (adduct.AdductIonName == "[M-H]-")
                {
                    // seek 241.0024 [C6H9O8S]-
                    var threshold1 = 0.1;
                    var diagnosticMz1 = 241.0024;
                    // seek 96.9601 [H2SO4-H]-
                    var threshold2 = 0.1;
                    var diagnosticMz2 = 96.9601;

                    var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                    var isClassIon2Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz2, threshold2);
                    if ((isClassIon1Found == false || isClassIon2Found == false)) return null;

                    // from here, acyl level annotation is executed.
                    var candidates = new List<LipidMolecule>();
                    for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                    {
                        for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                        {
                            if (sn1Carbon >= 26 && sn1Double >= 4) return null;
                            var sn2Carbon = totalCarbon - sn1Carbon;
                            var sn2Double = totalDoubleBond - sn1Double;

                            //var sn2 = fattyacidProductIon(sn2Carbon, sn2Double);
                            var NL_sn2 = theoreticalMz - acylCainMass(sn2Carbon, sn2Double) - H2O + Proton;

                            var query = new List<Peak> {
                            //new Peak() { Mz = sn2, Intensity = 0.1 },
                            new Peak() { Mz = NL_sn2, Intensity = 0.1 }
                        };

                            var foundCount = 0;
                            var averageIntensity = 0.0;
                            countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                            if (foundCount == 1)
                            { // now I set 2 as the correct level
                                var molecule = getEtherPhospholipidMoleculeObjAsLevel2("SMGDG", LbmClass.EtherSMGDG, sn1Carbon, sn1Double,
                                    sn2Carbon, sn2Double, averageIntensity, "e");
                                candidates.Add(molecule);
                            }
                        }
                    }
                    return returnAnnotationResult("SMGDG", LbmClass.EtherSMGDG, "", theoreticalMz, adduct,
                      totalCarbon, totalDoubleBond, 0, candidates, 2);
                }
            }
            else if (adduct.AdductIonName == "[M+NH4]+")
            {
                //seek M-SHex-H2O (M-277.0467551)
                var threshold1 = 1;
                var diagnosticMz1 = theoreticalMz - 277.0467551;
                var isClassIon1Found = isDiagnosticFragmentExist(spectrum, ms2Tolerance, diagnosticMz1, threshold1);
                if (isClassIon1Found == false) return null;

                // from here, acyl level annotation is executed.
                var candidates = new List<LipidMolecule>();
                for (int sn1Carbon = minSnCarbon; sn1Carbon <= maxSnCarbon; sn1Carbon++)
                {
                    for (int sn1Double = minSnDoubleBond; sn1Double <= maxSnDoubleBond; sn1Double++)
                    {
                        if (sn1Carbon >= 26 && sn1Double >= 4) return null;
                        var sn2Carbon = totalCarbon - sn1Carbon;
                        var sn2Double = totalDoubleBond - sn1Double;

                        var sn2 = fattyacidProductIon(sn2Carbon, sn2Double) - MassDiffDictionary.OxygenMass - Electron;
                        var sn2Mag = fattyacidProductIon(sn2Carbon, sn2Double) + 12 * 3 + MassDiffDictionary.OxygenMass + MassDiffDictionary.HydrogenMass * 5 + Proton;

                        var query = new List<Peak> {
                            new Peak() { Mz = sn2, Intensity = 0.1 },
                            new Peak() { Mz = sn2Mag, Intensity = 10 }
                        };

                        var foundCount = 0;
                        var averageIntensity = 0.0;
                        countFragmentExistence(spectrum, query, ms2Tolerance, out foundCount, out averageIntensity);

                        if (foundCount == 2)
                        { // now I set 2 as the correct level
                            var molecule = getEtherPhospholipidMoleculeObjAsLevel2("SMGDG", LbmClass.EtherSMGDG, sn1Carbon, sn1Double,
                                sn2Carbon, sn2Double, averageIntensity, "e");
                            candidates.Add(molecule);
                        }
                    }
                }
                return returnAnnotationResult("SMGDG", LbmClass.EtherSMGDG, "", theoreticalMz, adduct,
                  totalCarbon, totalDoubleBond, 0, candidates, 2);


            }
            return null;
        }



        // 
        private static LipidMolecule returnAnnotationResult(string lipidHeader, LbmClass lbmClass, 
            string hydrogenString, double theoreticalMz,
           AdductIon adduct, int totalCarbon, int totalDoubleBond, int totalOxidized, 
           List<LipidMolecule> candidates, int acylCountInMolecule) {
            if (candidates == null || candidates.Count == 0) {
                var annotationlevel = 1;
                if (acylCountInMolecule == 1) annotationlevel = 2;
                var result = getLipidAnnotaionAsSubLevel(lipidHeader, lbmClass, hydrogenString, totalCarbon, totalDoubleBond, totalOxidized, annotationlevel);
                result.Adduct = adduct;
                result.Mz = (float)theoreticalMz;
                return result;
            }
            else {
                var result = candidates.OrderByDescending(n => n.Score).ToList()[0];
                result.Adduct = adduct;
                result.Mz = (float)theoreticalMz;
                return result;
            }
        }

        private static bool isDiagnosticFragmentExist(ObservableCollection<double[]> spectrum, double ms2Tolerance,
            double diagnosticMz, double threshold) {
            for (int i = 0; i < spectrum.Count; i++) {
                var mz = spectrum[i][0];
                var intensity = spectrum[i][1]; // should be normalized by max intensity to 100

                if (intensity > threshold && Math.Abs(mz - diagnosticMz) < ms2Tolerance) {
                    return true;
                }
            }
            return false;
        }

        private static bool isPeakFoundWithACritetion(ObservableCollection<double[]> spectrum, double beginMz,
            double endMz, double threshold) {
            for (int i = 0; i < spectrum.Count; i++) {
                var mz = spectrum[i][0];
                var intensity = spectrum[i][1]; // should be normalized by max intensity to 100

                if (intensity > threshold && beginMz <= mz && mz <= endMz) {
                    return true;
                }
            }
            return false;
        }


        private static LipidMolecule getCeramideMoleculeObjAsLevel2(string lipidClass, LbmClass lbmClass, 
            string hydroxyString, //d: 2*OH, t: 3*OH
             int sphCarbon, int sphDouble, int acylCarbon, int acylDouble, double score) {

            var hydroxyString1 = hydroxyString;
            switch(hydroxyString)
            {
                case "m":
                    hydroxyString1 = ";O";
                    break;
                case "d":
                    hydroxyString1 = ";2O";
                    break;
                case "t":
                    hydroxyString1 = ";3O";
                    break;
            }
            //if (lipidClass == "AcylCer-BDS" || lipidClass == "HexCer-AP" || lipidClass == "AcylSM")  hydroxyString1 = "";

            //var totalCarbon = sphCarbon + acylCarbon;
            //var totalDB = sphDouble + acylDouble;
            //var totalString = totalCarbon + ":" + totalDB;
            //var totalName = lipidClass + " " + hydroxyString1 + totalString;
            //var sphChainString = hydroxyString.ToString() + sphCarbon.ToString() + ":" + sphDouble;
            //var acylChainString = acylCarbon + ":" + acylDouble;
            //var chainString = sphChainString + "/" + acylChainString;
            //var lipidName = lipidClass + " " + chainString;

            var totalCarbon = sphCarbon + acylCarbon;
            var totalDB = sphDouble + acylDouble;
            var totalString = totalCarbon + ":" + totalDB;
            var totalName = lipidClass + " " + totalString + hydroxyString1;
            var sphChainString =sphCarbon.ToString() + ":" + sphDouble + hydroxyString1;
            var acylChainString = acylCarbon + ":" + acylDouble;
            var chainString = sphChainString + "/" + acylChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule() {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sphCarbon,
                Sn1DoubleBondCount = sphDouble,
                Sn1AcylChainString = sphChainString,
                Sn2CarbonCount = acylCarbon,
                Sn2DoubleBondCount = acylDouble,
                Sn2AcylChainString = acylChainString
            };
        }

        private static LipidMolecule getNacylphospholipidMoleculeObjAsLevel2(string lipidClass, LbmClass lbmClass,
          int sn1Carbon, int sn1Double, int sn2Carbon, int sn2Double, double score) {

            var totalCarbon = sn1Carbon + sn2Carbon;
            var totalDB = sn1Double + sn2Double;
            var totalString = totalCarbon + ":" + totalDB;
            var totalName = lipidClass + " " + totalString;

            var sn1ChainString = sn1Carbon + ":" + sn1Double;
            var sn2ChainString = "N-" + sn2Carbon + ":" + sn2Double;
            var chainString = sn1ChainString + "/" + sn2ChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule() {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sn1Carbon,
                Sn1DoubleBondCount = sn1Double,
                Sn1AcylChainString = sn1ChainString,
                Sn2CarbonCount = sn2Carbon,
                Sn2DoubleBondCount = sn2Double,
                Sn2AcylChainString = sn2ChainString
            };
        }


        private static LipidMolecule getPhospholipidMoleculeObjAsLevel2(string lipidClass, LbmClass lbmClass,
            int sn1Carbon, int sn1Double, int sn2Carbon, int sn2Double, double score) {

            var totalCarbon = sn1Carbon + sn2Carbon;
            var totalDB = sn1Double + sn2Double;
            var totalString = totalCarbon + ":" + totalDB;
            var totalName = lipidClass + " " + totalString;

            //finally, acyl name ordering is determined by double bond count and acyl length
            var acyls = new List<int[]>() {
                new int[] { sn1Carbon, sn1Double }, new int[] { sn2Carbon, sn2Double }
            };
            acyls = acyls.OrderBy(n => n[1]).ThenBy(n => n[0]).ToList();
            var sn1CarbonCount = acyls[0][0];
            var sn1DbCount = acyls[0][1];
            var sn2CarbonCount = acyls[1][0];
            var sn2DbCount = acyls[1][1];

            #region finally, acyl name ordering is determined by double bond count and acyl length
            //if (sn1Double < sn2Double) {
            //    sn1CarbonCount = sn1Carbon;
            //    sn1DbCount = sn1Double;
            //    sn2CarbonCount = sn2Carbon;
            //    sn2DbCount = sn2Double;
            //}
            //else if (sn1Double > sn2Double) {
            //    sn1CarbonCount = sn2Carbon;
            //    sn1DbCount = sn2Double;
            //    sn2CarbonCount = sn1Carbon;
            //    sn2DbCount = sn1Double;
            //}
            //else if (sn1Carbon < sn2Carbon) {
            //    sn1CarbonCount = sn1Carbon;
            //    sn1DbCount = sn1Double;
            //    sn2CarbonCount = sn2Carbon;
            //    sn2DbCount = sn2Double;
            //}
            //else {
            //    sn1CarbonCount = sn2Carbon;
            //    sn1DbCount = sn2Double;
            //    sn2CarbonCount = sn1Carbon;
            //    sn2DbCount = sn1Double;
            //}
            #endregion

            var sn1ChainString = sn1CarbonCount + ":" + sn1DbCount;
            var sn2ChainString = sn2CarbonCount + ":" + sn2DbCount;
            var chainString = sn1ChainString + "_" + sn2ChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule() {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sn1CarbonCount,
                Sn1DoubleBondCount = sn1DbCount,
                Sn1AcylChainString = sn1ChainString,
                Sn2CarbonCount = sn2CarbonCount,
                Sn2DoubleBondCount = sn2DbCount,
                Sn2AcylChainString = sn2ChainString
            };
        }
        private static LipidMolecule getEtherPhospholipidMoleculeObjAsLevel2(string lipidClass, LbmClass lbmClass,
    int sn1Carbon, int sn1Double, int sn2Carbon, int sn2Double, double score, string chainSuffix)
        {
            var chainPrefix = chainSuffix;
            switch (chainSuffix)
            {
                case "e":
                    chainPrefix ="O-";
                    break;
                case "p":
                    chainPrefix = "P-";
                    break;
            }
            var totalCarbon = sn1Carbon + sn2Carbon;
            var totalDB = sn1Double + sn2Double;
            var totalString = totalCarbon + ":" + totalDB;
            //var totalName = lipidClass + " " + totalString + chainSuffix;
            var totalName = lipidClass + " " + chainPrefix + totalString ;

            //
            var acyls = new List<int[]>() {
                new int[] { sn1Carbon, sn1Double }, new int[] { sn2Carbon, sn2Double }
            };
            //acyls = acyls.OrderBy(n => n[1]).ThenBy(n => n[0]).ToList();
            var sn1CarbonCount = acyls[0][0];
            var sn1DbCount = acyls[0][1];
            var sn2CarbonCount = acyls[1][0];
            var sn2DbCount = acyls[1][1];


            //var sn1ChainString = sn1CarbonCount + ":" + sn1DbCount + chainSuffix;
            var sn1ChainString = chainPrefix + sn1CarbonCount + ":" + sn1DbCount;
            var sn2ChainString = sn2CarbonCount + ":" + sn2DbCount;
            //var chainString = sn1ChainString + "/" + sn2ChainString;
            var chainString = sn1ChainString + "_" + sn2ChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sn1CarbonCount,
                Sn1DoubleBondCount = sn1DbCount,
                Sn1AcylChainString = sn1ChainString,
                Sn2CarbonCount = sn2CarbonCount,
                Sn2DoubleBondCount = sn2DbCount,
                Sn2AcylChainString = sn2ChainString
            };
        }

        private static LipidMolecule getEtherOxPxMoleculeObjAsLevel2(string lipidClass, LbmClass lbmClass,
            int sn1Carbon, int sn1Double, int sn2Carbon, int sn2Double, int sn1Oxydized, int sn2Oxydized, double score, string chainSuffix)
        {
            var chainPrefix = chainSuffix;
            switch (chainSuffix)
            {
                case "e":
                    chainPrefix = "O-";
                    break;
                case "p":
                    chainPrefix = "P-";
                    break;
            }

            var totalCarbon = sn1Carbon + sn2Carbon;
            var totalOxydized = sn1Oxydized + sn2Oxydized;
            var totalDB = sn1Double + sn2Double;
            //var totalString = totalCarbon + ":" + totalDB + chainSuffix + "+" + totalOxydized + "O";
            var totalString = chainPrefix + totalCarbon + ":" + totalDB  + ";" + totalOxydized + "O";
            var totalName = lipidClass + " " + totalString;
            //
            var acyls = new List<int[]>() {
                new int[] { sn1Carbon, sn1Double, sn1Oxydized }, new int[] { sn2Carbon, sn2Double, sn2Oxydized }
            };
            //acyls = acyls.OrderBy(n => n[1]).ThenBy(n => n[0]).ToList();
            var sn1CarbonCount = acyls[0][0];
            var sn1DbCount = acyls[0][1];
            var sn1OxydizedCount = acyls[0][2];

            var sn2CarbonCount = acyls[1][0];
            var sn2DbCount = acyls[1][1];
            var sn2OxydizedCount = acyls[1][2];

            var sn1OxydizedString = "";
            var sn2OxydizedString = "";

            //if (sn1OxydizedCount != 0) { sn1OxydizedString = "+" + sn1OxydizedCount + "O"; }
            //if (sn2OxydizedCount != 0) { sn2OxydizedString = "+" + sn2OxydizedCount + "O"; }
            if (sn1OxydizedCount != 0) { sn1OxydizedString = ";" + sn1OxydizedCount + "O"; }
            if (sn2OxydizedCount != 0) { sn2OxydizedString = ";" + sn2OxydizedCount + "O"; }

            //var sn1ChainString = sn1CarbonCount + ":" + sn1DbCount + chainSuffix + sn1OxydizedString;
            var sn1ChainString = chainPrefix + sn1CarbonCount + ":" + sn1DbCount + sn1OxydizedString;
            var sn2ChainString = sn2CarbonCount + ":" + sn2DbCount + sn2OxydizedString;
            //var chainString = sn1ChainString + "/" + sn2ChainString;
            var chainString = sn1ChainString + "_" + sn2ChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sn1CarbonCount,
                Sn1DoubleBondCount = sn1DbCount,
                Sn1AcylChainString = sn1ChainString,
                Sn2CarbonCount = sn2CarbonCount,
                Sn2DoubleBondCount = sn2DbCount,
                Sn2AcylChainString = sn2ChainString
            };
        }



        private static LipidMolecule getOxydizedPhospholipidMoleculeObjAsLevel2(string lipidClass, LbmClass lbmClass,
    int sn1Carbon, int sn1Double, int sn2Carbon, int sn2Double, int sn1Oxydized, int sn2Oxydized, double score)
        {
            var totalCarbon = sn1Carbon + sn2Carbon;
            var totalDB = sn1Double + sn2Double;
            var totalOxydized = sn1Oxydized + sn2Oxydized;
            var totalOxidizedString = totalOxydized > 1 ? ";" + totalOxydized + "O" : ";O";
            //var totalString = totalCarbon + ":" + totalDB + "+" + totalOxydized + "O";
            var totalString = totalCarbon + ":" + totalDB + totalOxidizedString;
            var totalName = lipidClass + " " + totalString;
            //
            var acyls = new List<int[]>() {
                new int[] { sn1Carbon, sn1Double, sn1Oxydized }, new int[] { sn2Carbon, sn2Double, sn2Oxydized }
            };
            if (sn1Oxydized == 0)
            {
                acyls = acyls.OrderBy(n => n[2]).ThenBy(n => n[1]).ThenBy(n => n[0]).ToList();
            }
            else
            {
                acyls = acyls.OrderBy(n => n[1]).ThenBy(n => n[0]).ToList();
            }
            
            var sn1CarbonCount = acyls[0][0];
            var sn1DbCount = acyls[0][1];
            var sn1OxydizedCount = acyls[0][2];

            var sn2CarbonCount = acyls[1][0];
            var sn2DbCount = acyls[1][1];
            var sn2OxydizedCount = acyls[1][2];

            var sn1OxydizedString = "";
            var sn2OxydizedString = "";

            //if (sn1OxydizedCount != 0) { sn1OxydizedString = "+" + sn1OxydizedCount + "O"; }
            //if (sn2OxydizedCount != 0) { sn2OxydizedString = "+" + sn2OxydizedCount + "O"; }
            if (sn1OxydizedCount != 0) { sn1OxydizedString = sn1OxydizedCount == 1 ? ";O" : ";" + sn1OxydizedCount + "O"; }
            if (sn2OxydizedCount != 0) { sn2OxydizedString = sn2OxydizedCount == 1 ? ";O" : ";" + sn2OxydizedCount + "O"; }

            var sn1ChainString = sn1CarbonCount + ":" + sn1DbCount + sn1OxydizedString;
            var sn2ChainString = sn2CarbonCount + ":" + sn2DbCount + sn2OxydizedString;
            //var chainString = sn1ChainString + "-" + sn2ChainString;
            var chainString = sn1ChainString + "_" + sn2ChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sn1CarbonCount,
                Sn1DoubleBondCount = sn1DbCount,
                Sn1AcylChainString = sn1ChainString,
                Sn2CarbonCount = sn2CarbonCount,
                Sn2DoubleBondCount = sn2DbCount,
                Sn2AcylChainString = sn2ChainString
            };
        }
        private static LipidMolecule getOxydizedPhospholipidMoleculeObjAsLevel1(string lipidClass, LbmClass lbmClass,
    int totalCarbon, int totalDB, int totalOxydized, double score)
        {
            var totalString = totalCarbon + ":" + totalDB;
            var totalName = lipidClass + " " + totalString;
            //var chainString = totalString + "+" + totalOxydized + "O";
            var chainString = totalString + ";" + totalOxydized + "O";
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
            };
        }

        private static LipidMolecule getSingleacylchainMoleculeObjAsLevel2(string lipidClass, LbmClass lbmClass,
            int sn1Carbon, int sn1Double, double score)
        {

            var totalCarbon = sn1Carbon;
            var totalDB = sn1Double;
            var totalString = totalCarbon + ":" + totalDB;
            var totalName = lipidClass + " " + totalString;

            var acyls = new List<int[]>() {
                new int[] { sn1Carbon, sn1Double }
            };
            var sn1CarbonCount = acyls[0][0];
            var sn1DbCount = acyls[0][1];

            var sn1ChainString = sn1CarbonCount + ":" + sn1DbCount;
            var chainString = sn1ChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sn1CarbonCount,
                Sn1DoubleBondCount = sn1DbCount,
                Sn1AcylChainString = sn1ChainString
            };
        }

        private static LipidMolecule getSingleacyloxMoleculeObjAsLevel1(string lipidClass, LbmClass lbmClass,
    int sn1Carbon, int sn1Double, int totalOxydized, double score)
        {
            var totalString = sn1Carbon + ":" + sn1Double;
            //var totalName = lipidClass + " " + totalString + "+" + totalOxydized + "O";
            var totalName = lipidClass + " " + totalString + ";" + totalOxydized + "O";

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = totalName,
                TotalCarbonCount = sn1Carbon,
                TotalDoubleBondCount = sn1Double,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sn1Carbon,
                Sn1DoubleBondCount = sn1Double,
                Sn1AcylChainString = totalString,
                TotalOxidizedCount = totalOxydized
            };
        }


        private static LipidMolecule getSingleacylchainwithsuffixMoleculeObjAsLevel2(string lipidClass, LbmClass lbmClass,
            int sn1Carbon, int sn1Double, double score, string chainSuffix)
        {

            var totalCarbon = sn1Carbon;
            var totalDB = sn1Double;
            var totalString = totalCarbon + ":" + totalDB;

            var totalName = lipidClass + " " + totalString + chainSuffix;

            var lipidName = totalName;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sn1Carbon,
                Sn1DoubleBondCount = sn1Double,
                Sn1AcylChainString = totalString
            };
        }

        private static LipidMolecule getTriacylglycerolMoleculeObjAsLevel2(string lipidClass, LbmClass lbmClass,
           int sn1Carbon, int sn1Double, int sn2Carbon, int sn2Double, int sn3Carbon, int sn3Double, double score) {

            var totalCarbon = sn1Carbon + sn2Carbon + sn3Carbon;
            var totalDB = sn1Double + sn2Double + sn3Double;
            var totalString = totalCarbon + ":" + totalDB;
            var totalName = lipidClass + " " + totalString;

            var acyls = new List<int[]>() {
                new int[] { sn1Carbon, sn1Double }, new int[] { sn2Carbon, sn2Double }, new int[] { sn3Carbon, sn3Double }
            };
            acyls = acyls.OrderBy(n => n[1]).ThenBy(n => n[0]).ToList();

            var sn1CarbonCount = acyls[0][0];
            var sn1DbCount = acyls[0][1];
            var sn2CarbonCount = acyls[1][0];
            var sn2DbCount = acyls[1][1];
            var sn3CarbonCount = acyls[2][0];
            var sn3DbCount = acyls[2][1];

            var sn1ChainString = sn1CarbonCount + ":" + sn1DbCount;
            var sn2ChainString = sn2CarbonCount + ":" + sn2DbCount;
            var sn3ChainString = sn3CarbonCount + ":" + sn3DbCount;
            //var chainString = sn1ChainString + "-" + sn2ChainString + "-" + sn3ChainString;
            var chainString = sn1ChainString + "_" + sn2ChainString + "_" + sn3ChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule() {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sn1CarbonCount,
                Sn1DoubleBondCount = sn1DbCount,
                Sn1AcylChainString = sn1ChainString,
                Sn2CarbonCount = sn2CarbonCount,
                Sn2DoubleBondCount = sn2DbCount,
                Sn2AcylChainString = sn2ChainString,
                Sn3CarbonCount = sn3CarbonCount,
                Sn3DoubleBondCount = sn3DbCount,
                Sn3AcylChainString = sn3ChainString
            };
        }
        //add MT
        private static LipidMolecule getEthertagMoleculeObjAsLevel2(string lipidClass, LbmClass lbmClass,
   int sn1Carbon, int sn1Double, int sn2Carbon, int sn2Double, int sn3Carbon, int sn3Double, double score)
        {
            var etherPrefix = "O-";
            var totalCarbon = sn1Carbon + sn2Carbon + sn3Carbon;
            var totalDB = sn1Double + sn2Double + sn3Double;
            var totalString = etherPrefix + totalCarbon + ":" + totalDB;
            var totalName = lipidClass + " " + totalString;
            var sn2Carbon2 = sn2Carbon; var sn2Double2 = sn2Double;
            var sn3Carbon2 = sn3Carbon; var sn3Double2 = sn3Double;

            if (sn2Carbon > sn3Carbon || sn2Double > sn3Double) {
                sn2Carbon2 = sn3Carbon; sn2Double2 = sn3Double;
                sn3Carbon2 = sn2Carbon; sn3Double2 = sn2Double;
            }

            var acyls = new List<int[]>() {
                new int[] { sn1Carbon, sn1Double }, new int[] { sn2Carbon2, sn2Double2 }, new int[] { sn3Carbon2, sn3Double2 }
            };

            var sn1CarbonCount = acyls[0][0];
            var sn1DbCount = acyls[0][1];
            var sn2CarbonCount = acyls[1][0];
            var sn2DbCount = acyls[1][1];
            var sn3CarbonCount = acyls[2][0];
            var sn3DbCount = acyls[2][1];

            //var sn1ChainString = sn1CarbonCount + ":" + sn1DbCount + "e";
            var sn1ChainString = etherPrefix + sn1CarbonCount + ":" + sn1DbCount ;
            var sn2ChainString = sn2CarbonCount + ":" + sn2DbCount;
            var sn3ChainString = sn3CarbonCount + ":" + sn3DbCount;
            //var chainString = sn1ChainString + "-" + sn2ChainString + "-" + sn3ChainString;
            var chainString = sn1ChainString + "_" + sn2ChainString + "_" + sn3ChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sn1CarbonCount,
                Sn1DoubleBondCount = sn1DbCount,
                Sn1AcylChainString = sn1ChainString,
                Sn2CarbonCount = sn2CarbonCount,
                Sn2DoubleBondCount = sn2DbCount,
                Sn2AcylChainString = sn2ChainString,
                Sn3CarbonCount = sn3CarbonCount,
                Sn3DoubleBondCount = sn3DbCount,
                Sn3AcylChainString = sn3ChainString
            };
        }

    //    private static LipidMolecule getCeramideMoleculeObjAsLevel2_0(string lipidClass, LbmClass lbmClass,
    //string hydroxyString, //d: 2*OH, t: 3*OH
    //int totalCarbon, int totalDoubleBond, double score)
    //    {
    //        var totalDB = totalDoubleBond;
    //        var totalString = totalCarbon + ":" + totalDB;
    //        var totalName = lipidClass + " " + hydroxyString + totalString;

    //        var lipidName = totalName;

    //        return new LipidMolecule()
    //        {
    //            LipidClass = lbmClass,
    //            AnnotationLevel = 2,
    //            SublevelLipidName = totalName,
    //            LipidName = lipidName,
    //            TotalCarbonCount = totalCarbon,
    //            TotalDoubleBondCount = totalDB,
    //            TotalChainString = totalString,
    //            Score = score,
    //            //Sn1CarbonCount = sphCarbon,
    //            //Sn1DoubleBondCount = sphDouble,
    //            //Sn1AcylChainString = sphChainString,
    //            //Sn2CarbonCount = acylCarbon,
    //            //Sn2DoubleBondCount = acylDouble,
    //            //Sn2AcylChainString = acylChainString
    //        };
    //    }

        private static LipidMolecule getEsterceramideMoleculeObjAsLevel2(string lipidClass, LbmClass lbmClass,
    string hydroxyString, //d: 2*OH, t: 3*OH   Cer-EOS, EODS, EBDS, Hexcer-EOS
     int sphCarbon, int sphDouble, int acylCarbon, int acylDouble, int esterCarbon, int esterDouble, double score)
        {
            var sphHydroxyCount = 0;
            switch (hydroxyString)
            {
                case "m":
                    sphHydroxyCount = 1;
                    break;
                case "d":
                    sphHydroxyCount = 2;
                    break;
                case "t":
                    sphHydroxyCount = 3;
                    break;
            }

            //var totalCarbon = sphCarbon + acylCarbon + esterCarbon;
            //var totalDB = sphDouble + acylDouble + esterDouble;
            //var totalString = totalCarbon + ":" + totalDB;
            //var totalName = lipidClass + " " + hydroxyString + totalString;
            //var sphChainString = hydroxyString.ToString() + sphCarbon.ToString() + ":" + sphDouble;
            //var acylChainString = acylCarbon + ":" + acylDouble;
            //var esterChainString = esterCarbon + ":" + esterDouble;

            // standerd output

            var totalCarbon = sphCarbon + acylCarbon + esterCarbon;
            var totalDB = sphDouble + acylDouble + (esterDouble + 1);
            var totalString = totalCarbon + ":" + totalDB;
            var totalName = lipidClass+" " + totalString + ";" + (sphHydroxyCount +2).ToString() + "O";

            var sphChainString = sphCarbon.ToString() + ":" + sphDouble + ";" + sphHydroxyCount.ToString() + "O";
            var acylChainString = acylCarbon + ":" + acylDouble + ";O";
            if (lbmClass == LbmClass.Cer_EBDS) {
                acylChainString = acylCarbon + ":" + acylDouble + ";(3OH)";
            }
            var esterChainString = "(FA "+esterCarbon + ":" + esterDouble+")";

            //var chainString = sphChainString + "/" + acylChainString + "-O-" + esterChainString;
            var chainString = sphChainString + "/" + acylChainString + esterChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sphCarbon,
                Sn1DoubleBondCount = sphDouble,
                Sn1AcylChainString = sphChainString,
                Sn2CarbonCount = acylCarbon,
                Sn2DoubleBondCount = acylDouble,
                Sn2AcylChainString = acylChainString,
                Sn3CarbonCount = esterCarbon,
                Sn3DoubleBondCount = esterDouble,
                Sn3AcylChainString = esterChainString
            };
        }

        private static LipidMolecule getEsterceramideMoleculeObjAsLevel2_0(string lipidClass, LbmClass lbmClass,
            string hydroxyString, //d: 2*OH, t: 3*OH
            int cerCarbon, int cerDouble, int esterCarbon, int esterDouble, double score)
        {
            var sphHydroxyCount = 0;
            switch (hydroxyString)
            {
                case "m":
                    sphHydroxyCount = 1;
                    break;
                case "d":
                    sphHydroxyCount = 2;
                    break;
                case "t":
                    sphHydroxyCount = 3;
                    break;
            }

            var hydroxyString1 = hydroxyString;
            //if (lipidClass == "AcylCer-BDS" || lipidClass == "HexCer-AP" || lipidClass == "AcylSM") hydroxyString1 = "";

            //var totalCarbon = cerCarbon + esterCarbon;
            //var totalDB = cerDouble + esterDouble;
            //var totalString = totalCarbon + ":" + totalDB;
            //var totalName = lipidClass + " " + hydroxyString + totalString;

            //var cerChainString = hydroxyString.ToString() + cerCarbon.ToString() + ":" + cerDouble;
            //var esterChainString = esterCarbon + ":" + esterDouble;

            //var chainString = cerChainString + "-O-" + esterChainString;
            //var lipidName = lipidClass + " " + chainString;
            // standerd output

            var totalCarbon = cerCarbon + esterCarbon;
            var totalDB = cerDouble + (esterDouble + 1);
            var totalString = totalCarbon + ":" + totalDB;
            var totalName = lipidClass + " " + totalString + ";" + (sphHydroxyCount + 2).ToString() + "O";

            var cerChainString = cerCarbon.ToString() + ":" + cerDouble + ";" + (sphHydroxyCount + 1).ToString() + "O";
            var esterChainString = "(FA " + esterCarbon + ":" + esterDouble + ")";

            var chainString = cerChainString + esterChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = cerCarbon,
                Sn1DoubleBondCount = cerDouble,
                Sn1AcylChainString = cerChainString,
                Sn3CarbonCount = esterCarbon,
                Sn3DoubleBondCount = esterDouble,
                Sn3AcylChainString = esterChainString
            };
        }

        private static LipidMolecule getEsterceramideMoleculeObjAsLevel2_1(string lipidClass, LbmClass lbmClass,
    string hydroxyString, //d: 2*OH, t: 3*OH
    int sphCarbon, int sphDouble, int acylCarbon, int acylDouble, double score)
        {
            var sphHydroxyCount = 0;
            switch (hydroxyString)
            {
                case "m":
                    sphHydroxyCount = 1;
                    break;
                case "d":
                    sphHydroxyCount = 2;
                    break;
                case "t":
                    sphHydroxyCount = 3;
                    break;
            }

            var totalCarbon = sphCarbon + acylCarbon;
            //var totalDB = sphDouble + (acylDouble + 1);
            var totalDB = sphDouble + acylDouble;
            var totalString = totalCarbon + ":" + totalDB;
            var totalName = lipidClass + " " + totalString + ";" + (sphHydroxyCount + 2).ToString() + "O";

            var sphChainString = sphCarbon.ToString() + ":" + sphDouble + ";" + sphHydroxyCount.ToString() + "O";
            //var acylChainString = acylCarbon.ToString() + ":" + (acylDouble + 1) + ";2O";
            var acylChainString = acylCarbon.ToString() + ":" + acylDouble.ToString() + ";2O";

            var chainString = sphChainString + "/" + acylChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sphCarbon,
                Sn1DoubleBondCount = sphDouble,
                Sn1AcylChainString = sphChainString,
                Sn2CarbonCount = acylCarbon,
                Sn2DoubleBondCount = acylDouble,
                Sn2AcylChainString = acylChainString
            };
        }


        private static LipidMolecule getAcylhexceramideMoleculeObjAsLevel2(string lipidClass, LbmClass lbmClass,
            string hydroxyString, //d: 2*OH, t: 3*OH    AHexCer
            int sphCarbon, int sphDouble, int acylCarbon, int acylDouble, int esterCarbon, int esterDouble, double score, string acylHydroString)
        {
            var sphHydroxyCount = 0;
            switch (hydroxyString)
            {
                case "m":
                    sphHydroxyCount = 1;
                    break;
                case "d":
                    sphHydroxyCount = 2;
                    break;
                case "t":
                    sphHydroxyCount = 3;
                    break;
            }

            var totalCarbon = sphCarbon + acylCarbon + esterCarbon;
            var totalDB = sphDouble + acylDouble + esterDouble;
            var totalString = totalCarbon + ":" + totalDB;
            //var totalName = lipidClass + " " + hydroxyString + totalString + acylHydroString;
            var totalName = lipidClass + " " + totalString + ";" + (sphHydroxyCount+1).ToString() + "O";


            //var sphChainString = hydroxyString.ToString() + sphCarbon.ToString() + ":" + sphDouble;
            var sphChainString = sphCarbon.ToString() + ":" + sphDouble + ";" + sphHydroxyCount.ToString() + "O";
            var acylChainString = acylCarbon + ":" + acylDouble + ";O";
            //var esterChainString = esterCarbon + ":" + esterDouble;
            var esterChainString = "(O-" + esterCarbon + ":" + esterDouble + ")";


            //var chainString = esterChainString + "/" + sphChainString + "/" + acylChainString;
            var chainString = esterChainString +  sphChainString + "/" + acylChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sphCarbon,
                Sn1DoubleBondCount = sphDouble,
                Sn1AcylChainString = sphChainString,
                Sn2CarbonCount = acylCarbon,
                Sn2DoubleBondCount = acylDouble,
                Sn2AcylChainString = acylChainString,
                Sn3CarbonCount = esterCarbon,
                Sn3DoubleBondCount = esterDouble,
                Sn3AcylChainString = esterChainString
            };
        }

        private static LipidMolecule getAcylhexceramideMoleculeObjAsLevel2_0(string lipidClass, LbmClass lbmClass,
            string hydroxyString, //d: 2*OH, t: 3*OH
            int ceramideCarbon, int ceramideDouble, int esterCarbon, int esterDouble, double score, string acylHydroString)
        {
            var sphHydroxyCount = 0;
            switch (hydroxyString)
            {
                case "m":
                    sphHydroxyCount = 1;
                    break;
                case "d":
                    sphHydroxyCount = 2;
                    break;
                case "t":
                    sphHydroxyCount = 3;
                    break;
            }

            var totalCarbon = ceramideCarbon + esterCarbon;
            var totalDB = ceramideDouble + esterDouble;
            var totalString = totalCarbon + ":" + totalDB;
            //var totalName = lipidClass + " " + hydroxyString + totalString + acylHydroString;
            var totalName = lipidClass + " " + totalString + ";" + (sphHydroxyCount + 1).ToString() + "O";

            //var ceramideString = hydroxyString.ToString() + ceramideCarbon + ":" + ceramideDouble + acylHydroString;
            //var esterChainString = esterCarbon + ":" + esterDouble;
            var ceramideString = ceramideCarbon + ":" + ceramideDouble + ";" + (sphHydroxyCount + 1).ToString() + "O";
            var esterChainString = "(O-" + esterCarbon + ":" + esterDouble + ")";

            //var chainString = esterChainString + "/" + ceramideString;
            var chainString = esterChainString + ceramideString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule() {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = ceramideCarbon,
                Sn1DoubleBondCount = ceramideDouble,
                Sn1AcylChainString = ceramideString,
                Sn2CarbonCount = esterCarbon,
                Sn2DoubleBondCount = esterDouble,
                Sn2AcylChainString = esterChainString
            };
        }

        private static LipidMolecule getAcylhexceramideMoleculeObjAsLevel2_1(string lipidClass, LbmClass lbmClass,
             string hydroxyString, //d: 2*OH, t: 3*OH
             int sphCarbon, int sphDouble, int acylCarbon, int acylDouble, double score, string acylHydroString)
        {
            var sphHydroxyCount = 0;
            switch (hydroxyString)
            {
                case "m":
                    sphHydroxyCount = 1;
                    break;
                case "d":
                    sphHydroxyCount = 2;
                    break;
                case "t":
                    sphHydroxyCount = 3;
                    break;
            }

            var totalCarbon = sphCarbon + acylCarbon;
            var totalDB = sphDouble + acylDouble;
            var totalString = totalCarbon + ":" + totalDB;
            //var totalName = lipidClass + " " + hydroxyString + totalString + acylHydroString;
            var totalName = lipidClass + " " + totalString + ";" + (sphHydroxyCount + 1).ToString() + "O";

            //var sphChainString = hydroxyString.ToString() + sphCarbon.ToString() + ":" + sphDouble;
            //var acylChainString = acylCarbon + ":" + acylDouble + acylHydroString;
            var sphChainString = sphCarbon.ToString() + ":" + sphDouble + ";" + sphHydroxyCount.ToString() + "O";
            var acylChainString = acylCarbon + ":" + acylDouble + ";O";

            var chainString = sphChainString + "/" + acylChainString;
            //var lipidName = lipidClass + " " + chainString;
            var lipidName = lipidClass + " " + totalString + ";" + (sphHydroxyCount + 1).ToString() + "O"; // 

            return new LipidMolecule() {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sphCarbon,
                Sn1DoubleBondCount = sphDouble,
                Sn1AcylChainString = sphChainString,
                Sn2CarbonCount = acylCarbon,
                Sn2DoubleBondCount = acylDouble,
                Sn2AcylChainString = acylChainString,
            };
        }

        private static LipidMolecule getAsmMoleculeObjAsLevel2(string lipidClass, LbmClass lbmClass,
    string hydroxyString, //d: 2*OH, t: 3*OH   ASM neg
     int sphCarbon, int sphDouble, int acylCarbon, int acylDouble, int esterCarbon, int esterDouble, double score)
        {
            var sphHydroxyCount = 0;
            switch (hydroxyString)
            {
                case "m":
                    sphHydroxyCount = 1;
                    break;
                case "d":
                    sphHydroxyCount = 2;
                    break;
                case "t":
                    sphHydroxyCount = 3;
                    break;
            }

            var hydroxyString1 = hydroxyString;
            //if (lipidClass == "AcylCer-BDS" || lipidClass == "HexCer-AP" || lipidClass == "AcylSM") hydroxyString1 = "";

            var totalCarbon = sphCarbon + acylCarbon + esterCarbon;
            var totalDB = sphDouble + acylDouble + esterDouble;
            var totalString = totalCarbon + ":" + totalDB;
            //var totalName = lipidClass + " " + hydroxyString1 + totalString;
            var totalName = lipidClass + " " + totalString + ";" + (sphHydroxyCount + 1).ToString() + "O";


            //var sphChainString = hydroxyString.ToString() + sphCarbon.ToString() + ":" + sphDouble;
            //var acylChainString = acylCarbon + ":" + acylDouble;
            //var esterChainString = esterCarbon + ":" + esterDouble;
            var sphChainString = sphCarbon.ToString() + ":" + sphDouble + ";" + sphHydroxyCount.ToString() + "O";
            var acylChainString = acylCarbon + ":" + acylDouble;
            var esterChainString = "(FA " + esterCarbon + ":" + esterDouble + ")";

            //var chainString = sphChainString + "-O-" + esterChainString + "/" + acylChainString;
            //var lipidName = lipidClass + " " + chainString;
            var chainString = sphChainString + "/" + acylChainString + esterChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sphCarbon,
                Sn1DoubleBondCount = sphDouble,
                Sn1AcylChainString = sphChainString,
                Sn2CarbonCount = acylCarbon,
                Sn2DoubleBondCount = acylDouble,
                Sn2AcylChainString = acylChainString,
                Sn3CarbonCount = esterCarbon,
                Sn3DoubleBondCount = esterDouble,
                Sn3AcylChainString = esterChainString
            };
        }

        private static LipidMolecule getAsmMoleculeObjAsLevel2_0(string lipidClass, LbmClass lbmClass,
        string hydroxyString, //d: 2*OH, t: 3*OH   ASM neg
        int cerCarbon, int cerDouble, int esterCarbon, int esterDouble, double score)
        {
            var sphHydroxyCount = 0;
            switch (hydroxyString)
            {
                case "m":
                    sphHydroxyCount = 1;
                    break;
                case "d":
                    sphHydroxyCount = 2;
                    break;
                case "t":
                    sphHydroxyCount = 3;
                    break;
            }

            var hydroxyString1 = hydroxyString;
            //if (lipidClass == "AcylCer-BDS" || lipidClass == "HexCer-AP" || lipidClass == "AcylSM") hydroxyString1 = "";

            var totalCarbon = cerCarbon + esterCarbon;
            var totalDB = cerDouble + (esterDouble + 1);
            var totalString = totalCarbon + ":" + totalDB;
            var totalName = lipidClass + " " + totalString + ";" + (sphHydroxyCount + 1).ToString() + "O";

            var cerChainString = cerCarbon.ToString() + ":" + cerDouble + ";" + sphHydroxyCount.ToString() + "O";
            var esterChainString = "(FA " + esterCarbon + ":" + esterDouble + ")";

            var chainString = cerChainString + esterChainString;
            var lipidName = lipidClass + " " + chainString;


            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = cerCarbon,
                Sn1DoubleBondCount = cerDouble,
                Sn1AcylChainString = cerChainString,
                Sn3CarbonCount = esterCarbon,
                Sn3DoubleBondCount = esterDouble,
                Sn3AcylChainString = esterChainString
            };
        }


        private static LipidMolecule getAcylglycerolMoleculeObjAsLevel2(string lipidClass, LbmClass lbmClass,
   int sn1Carbon, int sn1Double, int sn2Carbon, int sn2Double, int sn3Carbon, int sn3Double, double score)
        {

            var totalCarbon = sn1Carbon + sn2Carbon + sn3Carbon;
            var totalDB = sn1Double + sn2Double + sn3Double;
            var totalString = totalCarbon + ":" + totalDB;
            var totalName = lipidClass + " " + totalString;

            var acyls = new List<int[]>() {
                new int[] { sn2Carbon, sn2Double }, new int[] { sn3Carbon, sn3Double }
            };
            acyls = acyls.OrderBy(n => n[1]).ThenBy(n => n[0]).ToList();

            var sn2CarbonCount = acyls[0][0];
            var sn2DbCount = acyls[0][1];
            var sn3CarbonCount = acyls[1][0];
            var sn3DbCount = acyls[1][1];

            var sn1ChainString = sn1Carbon + ":" + sn1Double;
            var sn2ChainString = sn2CarbonCount + ":" + sn2DbCount;
            var sn3ChainString = sn3CarbonCount + ":" + sn3DbCount;
            //var chainString = sn1ChainString + "/" + sn2ChainString + "-" + sn3ChainString;
            var chainString = sn1ChainString + "/" + sn2ChainString + "_" + sn3ChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sn1Carbon,
                Sn1DoubleBondCount = sn1Double,
                Sn1AcylChainString = sn1ChainString,
                Sn2CarbonCount = sn2CarbonCount,
                Sn2DoubleBondCount = sn2DbCount,
                Sn2AcylChainString = sn2ChainString,
                Sn3CarbonCount = sn3CarbonCount,
                Sn3DoubleBondCount = sn3DbCount,
                Sn3AcylChainString = sn3ChainString
            };
        }

        private static LipidMolecule getAdggaMoleculeObjAsLevel2(string lipidClass, LbmClass lbmClass,
        int sn1Carbon, int sn1Double, int sn2Carbon, int sn2Double, int sn3Carbon, int sn3Double, double score)
        {
            var totalCarbon = sn1Carbon + sn2Carbon + sn3Carbon;
            var totalDB = sn1Double + sn2Double + sn3Double;
            var totalString = totalCarbon + ":" + totalDB;
            var totalName = lipidClass + " " + totalString;

            var acyls = new List<int[]>() {
                new int[] { sn2Carbon, sn2Double }, new int[] { sn3Carbon, sn3Double }
            };
            acyls = acyls.OrderBy(n => n[1]).ThenBy(n => n[0]).ToList();

            var sn2CarbonCount = acyls[0][0];
            var sn2DbCount = acyls[0][1];
            var sn3CarbonCount = acyls[1][0];
            var sn3DbCount = acyls[1][1];

            //var sn1ChainString = sn1Carbon + ":" + sn1Double;
            var sn1ChainString = "(O-" +sn1Carbon + ":" + sn1Double +")";
            var sn2ChainString = sn2CarbonCount + ":" + sn2DbCount;
            var sn3ChainString = sn3CarbonCount + ":" + sn3DbCount;
            //var chainString = sn1ChainString + "/" + sn2ChainString + "-" + sn3ChainString;
            var chainString = sn1ChainString  + sn2ChainString + "_" + sn3ChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sn1Carbon,
                Sn1DoubleBondCount = sn1Double,
                Sn1AcylChainString = sn1ChainString,
                Sn2CarbonCount = sn2CarbonCount,
                Sn2DoubleBondCount = sn2DbCount,
                Sn2AcylChainString = sn2ChainString,
                Sn3CarbonCount = sn3CarbonCount,
                Sn3DoubleBondCount = sn3DbCount,
                Sn3AcylChainString = sn3ChainString
            };
        }



        private static LipidMolecule getDiacylglycerolMoleculeObjAsLevel2(string lipidClass, LbmClass lbmClass,
           int sn1Carbon, int sn1Double, int sn2Carbon, int sn2Double, double score)
        {

            var totalCarbon = sn1Carbon + sn2Carbon; 
            var totalDB = sn1Double + sn2Double; 
            var totalString = totalCarbon + ":" + totalDB;
            var totalName = lipidClass + " " + totalString;

            var acyls = new List<int[]>() {
                new int[] { sn1Carbon, sn1Double }, new int[] { sn2Carbon, sn2Double } 
            };
            acyls = acyls.OrderBy(n => n[1]).ThenBy(n => n[0]).ToList();

            var sn1CarbonCount = acyls[0][0];
            var sn1DbCount = acyls[0][1];
            var sn2CarbonCount = acyls[1][0];
            var sn2DbCount = acyls[1][1];
            var sn1ChainString = sn1CarbonCount + ":" + sn1DbCount;
            var sn2ChainString = sn2CarbonCount + ":" + sn2DbCount;
            //var chainString = sn1ChainString + "-" + sn2ChainString;
            var chainString = sn1ChainString + "_" + sn2ChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sn1CarbonCount,
                Sn1DoubleBondCount = sn1DbCount,
                Sn1AcylChainString = sn1ChainString,
                Sn2CarbonCount = sn2CarbonCount,
                Sn2DoubleBondCount = sn2DbCount,
                Sn2AcylChainString = sn2ChainString,
            };
        }

        private static LipidMolecule getCardiolipinMoleculeObjAsLevel2_0(string lipidClass, LbmClass lbmClass,
        int sn1_2Carbon, int sn1_2Double, int sn3_4Carbon, int sn3_4Double, double score)
        {

            var totalCarbon = sn1_2Carbon + sn3_4Carbon;
            var totalDB = sn1_2Double + sn3_4Double;
            var totalString = totalCarbon + ":" + totalDB;
            var totalName = lipidClass + " " + totalString;

            //
            var acyls = new List<int[]>() {
                new int[] { sn1_2Carbon, sn1_2Double }, new int[] { sn3_4Carbon, sn3_4Double }
            };
            acyls = acyls.OrderBy(n => n[1]).ThenBy(n => n[0]).ToList();
            var sn1CarbonCount = acyls[0][0];
            var sn1DbCount = acyls[0][1];
            var sn2CarbonCount = acyls[1][0];
            var sn2DbCount = acyls[1][1];


            var sn1ChainString = sn1CarbonCount + ":" + sn1DbCount;
            var sn2ChainString = sn2CarbonCount + ":" + sn2DbCount;
            //var chainString = sn1ChainString + "-" + sn2ChainString;
            var chainString = sn1ChainString + "_" + sn2ChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sn1CarbonCount,
                Sn1DoubleBondCount = sn1DbCount,
                Sn1AcylChainString = sn1ChainString,
                Sn2CarbonCount = sn2CarbonCount,
                Sn2DoubleBondCount = sn2DbCount,
                Sn2AcylChainString = sn2ChainString
            };
        }

        private static LipidMolecule getCardiolipinMoleculeObjAsLevel2_1(string lipidClass, LbmClass lbmClass,
        int sn1Carbon, int sn2Carbon, int sn3Carbon, int sn4Carbon, int sn1Double, int sn2Double, int sn3Double, int sn4Double, double score)
        {

            var totalCarbon = sn1Carbon + sn2Carbon + sn3Carbon + sn4Carbon;
            var totalDB = sn1Double + sn2Double + sn3Double + sn4Double;
            var totalString = totalCarbon + ":" + totalDB;
            var totalName = lipidClass + " " + totalString;

            //
            var acyls = new List<int[]>() {
                new int[] { sn1Carbon, sn1Double }, new int[] { sn2Carbon, sn2Double }, new int[] { sn3Carbon, sn3Double }, new int[] { sn4Carbon, sn4Double }};
            acyls = acyls.OrderBy(n => n[1]).ThenBy(n => n[0]).ToList();
            var sn1CarbonCount = acyls[0][0];
            var sn1DbCount = acyls[0][1];
            var sn2CarbonCount = acyls[1][0];
            var sn2DbCount = acyls[1][1];
            var sn3CarbonCount = acyls[0][0];
            var sn3DbCount = acyls[0][1];
            var sn4CarbonCount = acyls[1][0];
            var sn4DbCount = acyls[1][1];

 

            var sn1ChainString = sn1CarbonCount + ":" + sn1DbCount;
            var sn2ChainString = sn2CarbonCount + ":" + sn2DbCount;
            var sn3ChainString = sn3CarbonCount + ":" + sn3DbCount;
            var sn4ChainString = sn4CarbonCount + ":" + sn4DbCount;

            //var chainString = sn1ChainString + "-" + sn2ChainString + "-" + sn3ChainString + "-" + sn4ChainString;
            var chainString = sn1ChainString + "_" + sn2ChainString + "_" + sn3ChainString + "_" + sn4ChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sn1CarbonCount,
                Sn1DoubleBondCount = sn1DbCount,
                Sn1AcylChainString = sn1ChainString,
                Sn2CarbonCount = sn2CarbonCount,
                Sn2DoubleBondCount = sn2DbCount,
                Sn2AcylChainString = sn2ChainString,
                Sn3CarbonCount = sn3CarbonCount,
                Sn3DoubleBondCount = sn3DbCount,
                Sn3AcylChainString = sn3ChainString,
                Sn4CarbonCount = sn4CarbonCount,
                Sn4DoubleBondCount = sn4DbCount,
                Sn4AcylChainString = sn4ChainString

            };
        }
        private static LipidMolecule getCardiolipinMoleculeObjAsLevel2_2(string lipidClass, LbmClass lbmClass,
        int sn1Carbon, int sn2Carbon, int sn3Carbon, int sn4Carbon, int sn1Double, int sn2Double, int sn3Double, int sn4Double, double score)
        {

            var totalCarbon = sn1Carbon + sn2Carbon + sn3Carbon + sn4Carbon;
            var totalDB = sn1Double + sn2Double + sn3Double + sn4Double;
            var totalString = totalCarbon + ":" + totalDB;
            var totalName = lipidClass + " " + totalString;

            //
            var acyls = new List<int[]>() {
                new int[] { sn1Carbon, sn1Double }, new int[] { sn2Carbon, sn2Double } };
            acyls = acyls.OrderBy(n => n[1]).ThenBy(n => n[0]).ToList();

            var acyls2 = new List<int[]>() {
                new int[] { sn3Carbon, sn3Double }, new int[] { sn4Carbon, sn4Double }
            };
            acyls2 = acyls2.OrderBy(n => n[1]).ThenBy(n => n[0]).ToList();

            var acyls3 = new List<int[]>() {
                new int[] { sn1Carbon, sn1Double, sn2Carbon, sn2Double }, new int[] { sn3Carbon, sn3Double, sn4Carbon, sn4Double } };
            acyls3 = acyls3.OrderBy(n => n[1]).ThenBy(n => n[0]).ToList();

            var sn1CarbonCount = acyls3[0][0];
            var sn1DbCount = acyls3[0][1];
            var sn2CarbonCount = acyls3[0][2];
            var sn2DbCount = acyls3[0][3];

            var sn3CarbonCount = acyls3[1][0];
            var sn3DbCount = acyls3[1][1];
            var sn4CarbonCount = acyls3[1][2];
            var sn4DbCount = acyls3[1][3];


            var sn1ChainString = sn1CarbonCount + ":" + sn1DbCount;
            var sn2ChainString = sn2CarbonCount + ":" + sn2DbCount;

            var sn3ChainString = sn3CarbonCount + ":" + sn3DbCount;
            var sn4ChainString = sn4CarbonCount + ":" + sn4DbCount;

            //var chainString = sn1ChainString + "-" + sn2ChainString + "-" + sn3ChainString + "-" + sn4ChainString;
            var chainString = sn1ChainString + "_" + sn2ChainString + "_" + sn3ChainString + "_" + sn4ChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sn1CarbonCount,
                Sn1DoubleBondCount = sn1DbCount,
                Sn1AcylChainString = sn1ChainString,
                Sn2CarbonCount = sn2CarbonCount,
                Sn2DoubleBondCount = sn2DbCount,
                Sn2AcylChainString = sn2ChainString,
                Sn3CarbonCount = sn3CarbonCount,
                Sn3DoubleBondCount = sn3DbCount,
                Sn3AcylChainString = sn3ChainString,
                Sn4CarbonCount = sn4CarbonCount,
                Sn4DoubleBondCount = sn4DbCount,
                Sn4AcylChainString = sn4ChainString,

            };
        }

        private static LipidMolecule getLysocardiolipinMoleculeObjAsLevel2(string lipidClass, LbmClass lbmClass,
        int sn1Carbon, int sn2Carbon, int sn3Carbon, int sn1Double, int sn2Double, int sn3Double, double score)
        {

            var totalCarbon = sn1Carbon + sn2Carbon + sn3Carbon;
            var totalDB = sn1Double + sn2Double + sn3Double;
            var totalString = totalCarbon + ":" + totalDB;
            var totalName = lipidClass + " " + totalString;

            //
            var acyls = new List<int[]>() {
                new int[] { sn2Carbon, sn2Double }, new int[] { sn3Carbon, sn3Double }
            };
            acyls = acyls.OrderBy(n => n[1]).ThenBy(n => n[0]).ToList();
            var sn2CarbonCount = acyls[0][0];
            var sn2DbCount = acyls[0][1];
            var sn3CarbonCount = acyls[1][0];
            var sn3DbCount = acyls[1][1];


            var sn1ChainString = sn1Carbon + ":" + sn1Double;
            var sn2ChainString = sn2CarbonCount + ":" + sn2DbCount;
            var sn3ChainString = sn3CarbonCount + ":" + sn3DbCount;

            //var chainString = sn1ChainString + "/" + sn2ChainString + "-" + sn3ChainString;
            var chainString = sn1ChainString + "/" + sn2ChainString + "_" + sn3ChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sn1Carbon,
                Sn1DoubleBondCount = sn1Double,
                Sn1AcylChainString = sn1ChainString,
                Sn2CarbonCount = sn2CarbonCount,
                Sn2DoubleBondCount = sn2DbCount,
                Sn2AcylChainString = sn2ChainString,
                Sn3CarbonCount = sn3CarbonCount,
                Sn3DoubleBondCount = sn3DbCount,
                Sn3AcylChainString = sn3ChainString,

            };
        }

        private static LipidMolecule getFahfaMoleculeObjAsLevel2_0(string lipidClass, LbmClass lbmClass,
        int sn1Carbon, int sn1Double, int sn2Carbon, int sn2Double, double score)
        {

            var totalCarbon = sn1Carbon + sn2Carbon;
            var totalDB = sn1Double + sn2Double;
            var totalString = totalCarbon + ":" + totalDB;
            var totalName = lipidClass + " " + totalString;

            //
            var acyls = new List<int[]>() {
                new int[] { sn1Carbon, sn1Double }, new int[] { sn2Carbon, sn2Double }
            };
            //acyls = acyls.OrderBy(n => n[1]).ThenBy(n => n[0]).ToList();
            var sn1CarbonCount = acyls[0][0];
            var sn1DbCount = acyls[0][1];
            var sn2CarbonCount = acyls[1][0];
            var sn2DbCount = acyls[1][1];


            var sn1ChainString = sn1CarbonCount + ":" + sn1DbCount;
            var sn2ChainString = sn2CarbonCount + ":" + sn2DbCount;
            var chainString = sn1ChainString + "/" + sn2ChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sn1CarbonCount,
                Sn1DoubleBondCount = sn1DbCount,
                Sn1AcylChainString = sn1ChainString,
                Sn2CarbonCount = sn2CarbonCount,
                Sn2DoubleBondCount = sn2DbCount,
                Sn2AcylChainString = sn2ChainString
            };
        }

        private static LipidMolecule getFahfamideMoleculeObjAsLevel2(string lipidClass, LbmClass lbmClass,string surfix,
        int sn1Carbon, int sn1Double, int sn2Carbon, int sn2Double, double score)
        {

            var totalCarbon = sn1Carbon + sn2Carbon;
            var totalDB = sn1Double + sn2Double;
            var totalString = totalCarbon + ":" + totalDB;
            var totalName = lipidClass + " " + totalString+ surfix;

            //
            var acyls = new List<int[]>() {
                new int[] { sn1Carbon, sn1Double }, new int[] { sn2Carbon, sn2Double }
            };
            //acyls = acyls.OrderBy(n => n[1]).ThenBy(n => n[0]).ToList();
            var sn1CarbonCount = acyls[0][0];
            var sn1DbCount = acyls[0][1];
            var sn2CarbonCount = acyls[1][0];
            var sn2DbCount = acyls[1][1];


            var sn1ChainString = sn1CarbonCount + ":" + sn1DbCount;
            var sn2ChainString = sn2CarbonCount + ":" + sn2DbCount;
            var chainString = sn1ChainString + "/" + sn2ChainString;
            var lipidName = lipidClass + " " + chainString + surfix;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sn1CarbonCount,
                Sn1DoubleBondCount = sn1DbCount,
                Sn1AcylChainString = sn1ChainString,
                Sn2CarbonCount = sn2CarbonCount,
                Sn2DoubleBondCount = sn2DbCount,
                Sn2AcylChainString = sn2ChainString
            };
        }

        private static LipidMolecule getSteroidalEtherMoleculeObj(string lipidClass, LbmClass lbmClass,
    string steroidString, string steroidalModificationClass, int totalCarbon, int totalDB)
        {
            var totalString = string.Empty;
            var annotationLevel = 2;

            switch (steroidalModificationClass)
            {
                case "ester":
                    totalString = steroidString + "/" + totalCarbon + ":" + totalDB;
                    break;
                case "AHex":
                    totalString = steroidString + ";O;Hex;FA " + totalCarbon + ":" + totalDB;
                    break;
            }
            var totalName = lipidClass + " " + totalString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = annotationLevel,
                SublevelLipidName = totalName,
                LipidName = totalName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
            };

        }


        private static LipidMolecule getCeramideoxMoleculeObjAsLevel2(string lipidClass, LbmClass lbmClass,
    string hydroxyString, 
     int sphCarbon, int sphDouble, int acylCarbon, int acylDouble, int acylOxidized, double score)
            // Cer-Ax, Cer-Bx etc
        {
            var sphHydroxyCount = 0;
            var sphHydroxyString = "";
            switch (hydroxyString)
            {
                case "m":
                    sphHydroxyCount = 1;
                    sphHydroxyString = "O";
                    break;
                case "d":
                    sphHydroxyCount = 2;
                    sphHydroxyString = "2O";
                    break;
                case "t":
                    sphHydroxyCount = 3;
                    sphHydroxyString = "3O";
                    break;
            }
            var acylHydroxyString = "O";
            var lbmClassString = lbmClass.ToString();
            if (lbmClassString.Contains("_A"))
            {
                acylHydroxyString = "(2OH)";
            }
            else if (lbmClassString.Contains("_B") || lbmClassString.Contains("_EB"))
            {
                acylHydroxyString = "(3OH)";

            }
            //else if (lbmClassString.Contains("H"))
            //{
            //    acylHydroxyString = "O";
            //}

            var totalCarbon = sphCarbon + acylCarbon;
            var totalDB = sphDouble + acylDouble;
            //var totalString = acylOxidized == 0 ?
            //    hydroxyString + totalCarbon + ":" + totalDB :
            //    hydroxyString + totalCarbon + ":" + totalDB + "+" + "O";
            //var sphChainString = hydroxyString.ToString() + sphCarbon.ToString() + ":" + sphDouble;
            //var acylChainString = acylOxidized == 0 ?
            //    acylCarbon + ":" + acylDouble :
            //    acylCarbon + ":" + acylDouble + "+" + "O";
            //var chainString = sphChainString + "/" + acylChainString;

            var totalString = acylOxidized == 0 ?
                totalCarbon + ":" + totalDB + ";" + sphHydroxyString :
                totalCarbon + ":" + totalDB + ";" + (sphHydroxyCount+ acylOxidized).ToString() + "O";

            var totalName = lipidClass + " " + totalString;

            var sphChainString = sphCarbon.ToString() + ":" + sphDouble + ";" + sphHydroxyString;
            var acylChainString = acylOxidized == 0 ?
                acylCarbon + ":" + acylDouble :
                acylCarbon + ":" + acylDouble + ";" + acylHydroxyString;
            var chainString = sphChainString + "/" + acylChainString;
            var lipidName = lipidClass + " " + chainString;

            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = totalName,
                LipidName = lipidName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalChainString = totalString,
                Score = score,
                Sn1CarbonCount = sphCarbon,
                Sn1DoubleBondCount = sphDouble,
                Sn1AcylChainString = sphChainString,
                Sn2CarbonCount = acylCarbon,
                Sn2DoubleBondCount = acylDouble,
                Sn2AcylChainString = acylChainString
            };
        }




    //    private static LipidMolecule getLipidAnnotaionAsLevel1(string lipidClass, LbmClass lbmClass,
    //int totalCarbon, int totalDB,double score, string suffix)
    //    {
    //        var totalString = totalCarbon + ":" + totalDB + suffix;
    //        var totalName = lipidClass + " " + totalString;

    //        return new LipidMolecule()
    //        {
    //            LipidClass = lbmClass,
    //            AnnotationLevel = 2,
    //            SublevelLipidName = totalName,
    //            LipidName = totalName,
    //            TotalCarbonCount = totalCarbon,
    //            TotalDoubleBondCount = totalDB,
    //            TotalChainString = totalString,
    //            Score = score,
    //         };
    //    }


        private static LipidMolecule getLipidAnnotaionAsSubLevel(string lipidClass, LbmClass lbmClass,
            string hydroxyString, //d: 2*OH, t: 3*OH, string.empty: 0*OH
               int totalCarbon, int totalDB, int totalOxygen, int annotationLevel)
        {
            //var totalString = string.Empty;

            //if (hydroxyString == "e" || hydroxyString == "p")
            //{
            //    totalString = totalOxygen == 0 ?
            //                    totalCarbon + ":" + totalDB + hydroxyString :
            //                    totalCarbon + ":" + totalDB + hydroxyString + "+" + totalOxygen + "O";
            //}
            //else if (hydroxyString == "d" || hydroxyString == "t" || hydroxyString == "m")
            //{
            //    totalString = totalOxygen == 0 ?
            //                    hydroxyString + totalCarbon + ":" + totalDB :
            //                    hydroxyString + totalCarbon + ":" + totalDB + "+" + totalOxygen + "O";
            //}
            //else
            //{
            //    totalString = totalOxygen == 0 ?
            //                    totalCarbon + ":" + totalDB + hydroxyString :
            //                    totalCarbon + ":" + totalDB + hydroxyString + "+" + totalOxygen + "O";
            //}

            var totalOxygenString = totalOxygen == 0 ? string.Empty : totalOxygen == 1 ? ";O" : ";" + totalOxygen + "O";
            var totalString = totalCarbon + ":" + totalDB + hydroxyString + totalOxygenString;

            switch (hydroxyString)
            {
                case "e": totalString = "O-" + totalCarbon + ":" + totalDB + totalOxygenString;
                    break;
                case "p":
                    totalString = "P-" + totalCarbon + ":" + totalDB + totalOxygenString;
                    break;
                case "m":
                    totalString = totalOxygen == 0 ?
                        totalCarbon + ":" + totalDB + ";O":
                        totalCarbon + ":" + totalDB + ";" + (totalOxygen + 1).ToString() + "O";
                    break;
                case "d":
                    totalString = totalOxygen == 0 ?
                        totalCarbon + ":" + totalDB + ";2O" :
                        totalCarbon + ":" + totalDB + ";" + (totalOxygen + 2).ToString() + "O";
                    break;
                case "t":
                    totalString = totalOxygen == 0 ?
                        totalCarbon + ":" + totalDB + ";3O" :
                        totalCarbon + ":" + totalDB + ";" + (totalOxygen + 3).ToString() + "O";
                    break;
            }

            var totalName = lipidClass + " " + totalString;
            if (lipidClass.StartsWith("SE ") || lbmClass.ToString().EndsWith("CAE")) {
                totalName = lipidClass + "/" + totalString;
            }
            if (lipidClass.StartsWith("CE") && totalString == "0:0") {
                totalName = "Cholesterol";
            }

            return new LipidMolecule() {
                LipidClass = lbmClass,
                AnnotationLevel = annotationLevel,
                SublevelLipidName = totalName,
                LipidName = totalName,
                TotalCarbonCount = totalCarbon,
                TotalDoubleBondCount = totalDB,
                TotalOxidizedCount = totalOxygen,
                TotalChainString = totalString,
            };
        }
        private static LipidMolecule returnAnnotationNoChainResult(string lipidHeader, LbmClass lbmClass,
    string hydrogenString, double theoreticalMz,
   AdductIon adduct, int totalCarbon, int totalDoubleBond, int totalOxidized,
   List<LipidMolecule> candidates, int acylCountInMolecule)
        {
            return new LipidMolecule()
            {
                LipidClass = lbmClass,
                AnnotationLevel = 2,
                SublevelLipidName = lipidHeader,
                LipidName = lipidHeader,
                Adduct = adduct,
                Mz = (float)theoreticalMz
            };
        }



        private static void countFragmentExistence(ObservableCollection<double[]> spectrum, List<Peak> queries, double ms2Tolerance,
            out int foundCount, out double averageIntensity) {
            foundCount = 0;
            averageIntensity = 0.0;
            foreach (var query in queries) {
                foreach (var peak in spectrum) {
                    var mz = peak[0];
                    var intensity = peak[1]; // relative intensity
                    if (query.Intensity < intensity && Math.Abs(query.Mz - mz) < ms2Tolerance) {
                        foundCount++;
                        averageIntensity += intensity;
                        break;
                    }
                }
            }
            averageIntensity /= (double)queries.Count;
        }

        private static double acylCainMass(int carbon, int dbBond) {
            var hydrogenMass = (double)((carbon * 2) - 1 - (dbBond * 2)) * MassDiffDictionary.HydrogenMass;
            return (MassDiffDictionary.CarbonMass * (double)carbon) + hydrogenMass + MassDiffDictionary.OxygenMass;
        }

        private static double fattyacidProductIon(int carbon, int dbBond) {
            var hydrogenMass = (double)(carbon * 2 - 1 - dbBond * 2) * MassDiffDictionary.HydrogenMass;
            return MassDiffDictionary.CarbonMass * (double)carbon + hydrogenMass + MassDiffDictionary.OxygenMass * 2.0 + Electron;
        }
        private static double SphingoChainMass(int carbon, int dbBond)
        {
            var hydrogenMass = (double)(carbon * 2 - dbBond * 2) * MassDiffDictionary.HydrogenMass;
            return MassDiffDictionary.CarbonMass * (double)carbon + hydrogenMass + MassDiffDictionary.OxygenMass * 2 + MassDiffDictionary.NitrogenMass;
        }


    }
}
