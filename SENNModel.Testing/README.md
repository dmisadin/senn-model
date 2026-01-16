Compile Fortran using GNU: gfortran via MSYS2 (very common, free)

Install MSYS2. https://www.msys2.org

Open “MSYS2 MinGW x64” terminal (important: MinGW x64, not plain MSYS).

Install gfortran:
```bash
pacman -Syu
# close/reopen the terminal if it tells you to
pacman -S mingw-w64-x86_64-gcc-fortran
```

This package provides gfortran.exe. [Docs](https://packages.msys2.org/packages/mingw-w64-x86_64-gcc-fortran)
4) Compile and run:
```bash
gfortran mycode.f -o mycode.exe
./mycode.exe
```

Legacy-friendly flags (often useful with F77-era code):
```bash
gfortran -std=legacy -Wall -Wextra -fcheck=all mycode.f -o mycode.exe
```

General install guidance is also summarized by fortran-lang.

Quick test program (F77 style)

Save as hello.f:

      PROGRAM HELLO
      PRINT *, 'HELLO FROM FORTRAN 77'
      END


Compile with:
```bash
gfortran hello.f -o hello.exe
```

```bash
gfortran -std=legacy -ffixed-form -ffixed-line-length-none -O2 SENN.f -o SENN_recompiled.exe
```

While inside MSYS2 MINGW64 console to copy DLL files required to run `SENN_recompiled.exe`:

```bash
cp /mingw64/bin/libgfortran-5.dll .
cp /mingw64/bin/libquadmath-0.dll .
cp /mingw64/bin/libgcc_s_seh-1.dll .
cp /mingw64/bin/libwinpthread-1.dll .
```

## Compile .NET solution

Run this command in solution directory inside PowerShell:
```powershell
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true
```
Find the executable in `SENNModel\bin\Release\net8.0\win-x64\publish` directory.

Run the `compare_runs.py` with command:
```bash
python compare_runs.py --start 1 --end 3
```