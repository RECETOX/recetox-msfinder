﻿using Rfx.Riken.OsakaUniv;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Msdial.Lcms.Dataprocess {
    public sealed class IonMobilityUtility {
        private IonMobilityUtility() { }

        // import numpy as np
        // from scipy import constants
        // >>> T0 = 273.15
        // >>> p0 = 1.01325e5 # 1atm
        // >>> N0 = p0 / (constants.k * T0)
        // >>> (3.0/16.0) * (1/N0) * np.sqrt(2*np.pi/constants.k) * constants.e / np.sqrt(constants.u) / 1e-20 / 1e-4
        private const double ccs_conversion_factor = 18509.863216340458;

        public static double MobilityToCrossSection(IonMobilityType mobilitytype, double mobility, int charge, double molWeight, 
            CoefficientsForCcsCalculation calinfo, bool isCalibrantInfoImported,
            double gasWeight = 28.0134, double temperature = 305.0) {
            var reducedMass = molWeight * gasWeight / (molWeight + gasWeight);
            if (mobilitytype == IonMobilityType.Tims) {
                var ccs = ccs_conversion_factor * (double)charge / (Math.Sqrt(reducedMass * temperature) / mobility);
                if (double.IsPositiveInfinity(ccs)) {
                    return -1;
                }
                return ccs; // in Angstrom^2
            } else if (!isCalibrantInfoImported) { // for Agilent and Waters, mobility value should not be reversed.
                var ccs = ccs_conversion_factor * (double)charge / (Math.Sqrt(reducedMass * temperature) * mobility);
                if (double.IsPositiveInfinity(ccs)) {
                    return -1;
                }
                return ccs; // in Angstrom^2
            }
            else if (mobilitytype == IonMobilityType.Dtims) {
                var beta = calinfo.AgilentBeta;
                var tfix = calinfo.AgilentTFix;

                var ccs = (mobility - tfix) * charge / beta / Math.Sqrt(molWeight / (molWeight + gasWeight));
                return ccs;
            }
            else if (mobilitytype == IonMobilityType.Twims) {
                var coeff = calinfo.WatersCoefficient;
                var t0 = calinfo.WatersT0;
                var exponent = calinfo.WatersExponent;

                var ccs = coeff * Math.Pow(mobility + t0, exponent);
                return ccs;
            }
            else {
                return -1;
            }
        }

        public static double CrossSectionToMobility(IonMobilityType type, double ccs, int charge, double molWeight,
            CoefficientsForCcsCalculation calinfo, bool isCalibrantInfoImported,
            double gasWeight = 28.0134, double temperature = 305.0) {
           
            if (type == IonMobilityType.Tims) {
                var reducedMass = molWeight * gasWeight / (molWeight + gasWeight);
                var k0 = ccs_conversion_factor * (double)charge / (Math.Sqrt(reducedMass * temperature) * ccs);
                if (k0 > 0)
                    return 1 / k0;
                else
                    return -1;
            } else if (!isCalibrantInfoImported) {
                var reducedMass = molWeight * gasWeight / (molWeight + gasWeight);
                var k0 = ccs_conversion_factor * (double)charge / (Math.Sqrt(reducedMass * temperature) * ccs);
                return k0;	// in cm2/Vs
            }
            else if (type == IonMobilityType.Dtims) {
                var beta = calinfo.AgilentBeta;
                var tfix = calinfo.AgilentTFix;

                var k0 = ccs * beta * Math.Sqrt(molWeight / (molWeight + gasWeight)) / charge + tfix;
                return k0;
            }
            else if (type == IonMobilityType.Twims) {
                var coeff = calinfo.WatersCoefficient;
                var t0 = calinfo.WatersT0;
                var exponent = calinfo.WatersExponent;
                var k0 = Math.Pow(ccs / coeff, 1 / exponent) - t0;
               
                return k0;
            }
            else {
                return -1;
            }
        }
    }
}
