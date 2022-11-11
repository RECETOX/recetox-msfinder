# MsdialWorkbench contents

# MS-DIAL - software for untargeted metabolomics and lipidomics
The program supports data processings for any type of chromatography / scan type mass spectrometry data, and the assembly is licensed under the CC-BY 4.0.
Please contact Hiroshi Tsugawa (hiroshi.tsugawa@riken.jp) for feedback, bug reports, and questions.

# MS-FINDER - software for structure elucidation of unknown spectra with hydrogen rearrangement (HR) rules
The program supports molecular formula prediction, metabolie class prediction, and structure elucidation for EI-MS and MS/MS spectra, and the assembly is licensed under the CC-BY 4.0.
Please contact Hiroshi Tsugawa (hiroshi.tsugawa@riken.jp) for feedback, bug reports, and questions.

# Developers
Lead developer: Hiroshi Tsugawa (RIKEN) 
Current main developers: Hiroshi Tsugawa (RIKEN), Ipputa Tada (SOKENDAI), and Yuki Matsuzawa (RIKEN)
Past developers: Diego Pedrosa (UC Davis)

# Requirements:
- .net framework 4.8
- nuget.exe (https://www.nuget.org/downloads)

# Installation:
Clone the github directory
```
git clone https://github.com/RECETOX/recetox-msfinder.git
```

From the project root run 
```
nuget.exe restore
dotnet build .\MsfinderConsoleApp\ 
```

# Usage
## Running MsFinder:

**Required args:**
| ------------- | ------------- |
| -i | input folder/file to be processed |
| -m | method file holding processing properties |
| -o | output folder to save results |

**example:** 
```
MsfinderConsoleApp.exe annotate -i <input folder> -m <method file> -o <output file>

MsfinderConsoleApp.exe annotate -i test.msp -m MSFINDER.INI -o out.msp
```

# About LBM file in MS-DIAL project
The LBM (*.LBM2) file contains the in silico MS/MS spectra of lipids.
There are currently three files named with 'FiehnO (Oliver Fiehn laboratory)', 'AritaM (Makoto Arita laboratory)', and 'SaitoK (Kazuki Saito laboratory)'.
These files contain the same MS/MS spectra information but have different predicted retention times which were optimized for the indivisual method.
One of the '.LBM' files which contains lipid's in silico MS/MS should be also in the same folder as 'MSDIAL.exe' for Lipidomics project. 

# Further
MRMPROBS software suite is sutable for targeted metabolomics and lipidomics, and it also supports MRM/SRM data.
http://prime.psc.riken.jp/compms/mrmprobs/main.html


# Source code license
This is the source code for msdial version 4.18 and msfinder version 3.32.
The source code is licensed under GNU LESSER GENERAL PUBLIC LICENSE (LGPL) version 3.
See LGPL.txt for full text of the license.
This software uses third-party software.
A full list of third-party software licenses in MsdialWorkbench is in the file THIRD-PARTY-LICENSE-README.txt.




