﻿using org.openscience.cdk.exception;
using org.openscience.cdk.qsar.descriptors.molecular;
using Riken.Metabolomics.StructureFinder.Property;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Riken.Metabolomics.StructureFinder.Utility {
    public sealed class XlogpCalculator {

        private XlogpCalculator() { }

        public static double XlogP(Structure structure) {

            var xlogp = -1.0;
            var descriptor = new XLogPDescriptor();
            var parameters = new Object[2] { new java.lang.Boolean(true), new java.lang.Boolean(true) };

            try {
                descriptor.setParameters(parameters);
            }
            catch (CDKException ex) {
                Console.WriteLine(ex.ToString());
            }

            var xlogpString = string.Empty;
            try {
                xlogpString = descriptor.calculate(structure.IContainer).getValue().ToString();
            }
            catch (CDKException ex) {
                Console.WriteLine(ex.ToString());
            }
            catch (System.NullReferenceException ex) {
                Console.WriteLine(ex.ToString());
            }

            if (double.TryParse(xlogpString, out xlogp)) {
                return xlogp;
            }
            else {
                return -1.0;
            }
        }
    }
}
