# SENN Model Simulation

This repository contains an implementation and validation of the **Spatially Extended Non‑Linear Node (SENN) model**, a computational model used for simulating neural activation during electrical stimulation.

The goal of this project is **not only to run the original SENN simulation**, but to **verify numerical equivalence between a modern C# implementation and the original legacy Fortran code** described in the reference literature.

---

## Background

The SENN model originates from the book:

> *Electrostimulation: Theory, Applications, and Computational Model*  
> J. Patrick Reilly, Alan M. Diamant

The original implementation was written in **Fortran 77**, later compiled using Fortran 95 compilers in legacy mode. The executables distributed with the book include user interface elements that make them unsuitable for automated testing and batch simulations.

For this reason, the original Fortran source code is compiled directly, and its numerical output is used as the **reference baseline**.

---

## What this project does

This repository contains three main components:

1. **Original Fortran SENN simulation**
   - Compiled on Windows using GNU Fortran (gfortran) in legacy Fortran 77 mode.
   - Serves as the reference implementation.

2. **Modern C# (.NET) reimplementation**
   - A clean, maintainable version of the same mathematical model.
   - Designed for future extension, automation, and integration.

3. **Automated testing and comparison**
   - Python scripts execute both binaries with identical parameters.
   - Outputs are compared numerically and visually.
   - Resulting plots allow validation of correctness and detection of divergence.

The purpose of this workflow is to ensure that the C# model faithfully reproduces the behavior of the original Fortran simulation.

---

## Testing workflow overview

At a high level, the testing process is:

1. Run the SENN simulation using the original Fortran implementation.
2. Run the same simulation using the C# implementation.
3. Compare outputs across multiple simulation runs.
4. Generate plots and result files for analysis.

Detailed build and execution instructions are documented separately.

---

## Detailed testing instructions

Step‑by‑step instructions for compiling Fortran, building the .NET executable, and running Python comparison tests are located here:

**[`SENNModel.Testing/README.md`](SENNModel.Testing/README.md)**

That document describes:
- How the Fortran code is compiled on Windows
- How runtime dependencies are handled
- How the C# executable is built
- How automated comparison tests are executed

---

## Purpose of the repository

This project exists to:

- Preserve a historically important computational neuroscience model
- Validate correctness when porting legacy scientific code
- Enable modern experimentation using reliable, reproducible simulations
- Provide a foundation for future numerical and biomedical research

---

## Notes

- The Fortran code follows legacy Fortran 77 rules and behavior.
- Numerical differences between compilers are carefully analyzed.
- All comparisons are performed using identical simulation parameters.

This repository is intended for developers, researchers, and students who wish to understand, validate, or extend the SENN computational model using modern tools while remaining faithful to the original scientific work.
