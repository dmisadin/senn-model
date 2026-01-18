# Testing the output difference between new C# and old Fortran code

## 1. Compile Fortran using GNU: gfortran via MSYS2
Quote from original book `Electrostimulation: Theory, Applications, and Computational Model; J. Patrick Reilly, Alan M. Diamant`:
>The executables for Part II were created using PC and Macintosh compiler packages by the ABSOFT Software Development Corporation. 
>FileSENN.f is the Fortran source code. The code follows Fortran 77 rules, and was compiled by Fortran 95 compilers in Legacy mode.

The execurable that I found with this book contains UI element which poses an issue because the process does not shut down after the simulation is done. That is inconvenient for the purpose of this tests, so we will compile the source Fortran code ourselves.
The steps below are what I found to work on my Windows 10 machine.

### 1.1. Install MSYS2 

Follow installation steps in official [Docs](https://www.msys2.org).

### 1.2. Open “MSYS2 MinGW x64” terminal (important: MinGW x64, not plain MSYS).

Install gfortran:
```bash
pacman -Syu
# close/reopen the terminal if it tells you to
pacman -S mingw-w64-x86_64-gcc-fortran
```
This package provides gfortran.exe. [Docs](https://packages.msys2.org/packages/mingw-w64-x86_64-gcc-fortran)

### 1.3. Compile and run:

```bash
gfortran -std=legacy -ffixed-form -ffixed-line-length-none -O2 SENN.f -o SENN_fortran.exe
```

While inside MSYS2 MINGW64 console to copy DLL files required to run `SENN_fortran.exe`:
```bash
cp /mingw64/bin/libgfortran-5.dll .
cp /mingw64/bin/libquadmath-0.dll .
cp /mingw64/bin/libgcc_s_seh-1.dll .
cp /mingw64/bin/libwinpthread-1.dll .
```

## 2. Compile .NET solution

Run this command in solution directory inside PowerShell:
```powershell
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true
```
Find the executable in `SENNModel\bin\Release\net8.0\win-x64\publish` directory.

## 3. Run Python tests

### 3.1. Copy compiled executables to testing directory

Python script expects these executables to be inside its working directory: `SENN_fortran.exe` (+ corresponding `.dll` files) and `SENN_csharp.exe`.

### 3.2. Run the Python script
Install the required packages:
```bash
pip install matplotlib
```

Run the `compare_runs.py` with command (example shows how to run through directories `run_1` to `run_5`):
```bash
python compare_runs.py --start 1 --end 5
```

### 3.3. Observe the results

In corresponding directory to each run, (`run_N`, where N is ordinal number of a run) you will find output files with plotted graphs in image format.