#!/usr/bin/env python3
import argparse
import shutil
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
import matplotlib.pyplot as plt


FORTRAN_DEFAULT = "SENN_fortran.exe"
CSHARP_DEFAULT = "SENN_csharp.exe"

FORTRAN_OUTPUTS = ["fort.17", "fort.30", "data.out"]
CSHARP_OUTPUTS = ["plot_17.txt", "plot_30.txt", "data_out.txt"]


@dataclass(frozen=True)
class FileState:
    exists: bool
    size: int
    mtime: float

def run_csharp(exe: Path, workdir: Path, output_dir: Path, run_name: str, timeout: int | None) -> None:
    """
    Run C# exe with: --headless --output-dir run_N
    Captures stdout/stderr to per-run files.
    """
    if not exe.exists():
        raise RuntimeError(f"Missing C# executable: {exe}")

    stdout_path = workdir / f"{run_name}_SENNModel_stdout.txt"
    stderr_path = workdir / f"{run_name}_SENNModel_stderr.txt"

    cmd = [str(exe), "--headless", "--output-dir", str(output_dir)]

    with stdout_path.open("wb") as out, stderr_path.open("wb") as err:
        proc = subprocess.run(
            cmd,
            cwd=str(workdir),
            stdout=out,
            stderr=err,
            timeout=timeout,
            check=False,
        )

    if proc.returncode != 0:
        raise RuntimeError(
            f"C# run failed with exit code {proc.returncode}. "
            f"See {stdout_path.name} / {stderr_path.name}"
        )


def run_fortran(
    exe: Path,
    workdir: Path,
    run_name: str,
    timeout: int | None,
) -> None:
    """
    Run Fortran exe and wait for it to exit. Capture stdout/stderr to per-run files.
    """
    if not exe.exists():
        raise RuntimeError(f"Missing Fortran executable: {exe}")

    stdout_path = workdir / f"{run_name}_SENN_stdout.txt"
    stderr_path = workdir / f"{run_name}_SENN_stderr.txt"

    # Remove stale outputs before starting
    for f in FORTRAN_OUTPUTS:
        p = workdir / f
        if p.exists():
            p.unlink()

    with stdout_path.open("wb") as out, stderr_path.open("wb") as err:
        proc = subprocess.run(
            [str(exe)],
            cwd=str(workdir),
            stdout=out,
            stderr=err,
            timeout=timeout,
            check=False,
        )

    if proc.returncode != 0:
        raise RuntimeError(
            f"Fortran run failed with exit code {proc.returncode}. "
            f"See {stdout_path.name} / {stderr_path.name}"
        )

    # Optional: ensure outputs exist before continuing
    missing = [fn for fn in FORTRAN_OUTPUTS if not (workdir / fn).exists()]
    if missing:
        raise RuntimeError(f"Fortran finished but outputs are missing: {missing}")


def copy_files(src_dir: Path, dst_dir: Path, filenames: list[str]) -> None:
    dst_dir.mkdir(parents=True, exist_ok=True)
    for fn in filenames:
        src = src_dir / fn
        if not src.exists():
            raise RuntimeError(f"Expected output missing: {src}")
        shutil.copy2(src, dst_dir / fn)

def _parse_xy_file_iterations(path: Path, *, mode: str) -> list[list[tuple[float, float]]]:
    """
    Returns list of iterations, each iteration is list of (x,y) floats.

    mode:
      - "fortran": iterations separated by line '5000 5000' (separator ignored)
      - "csharp": line '0 0' marks the START of a new iteration (marker ignored)
    """
    if mode not in {"fortran", "csharp"}:
        raise ValueError("mode must be 'fortran' or 'csharp'")

    iterations: list[list[tuple[float, float]]] = []
    current: list[tuple[float, float]] = []

    def flush():
        nonlocal current
        if current:
            iterations.append(current)
            current = []

    with path.open("r", encoding="utf-8", errors="replace") as f:
        for raw in f:
            line = raw.strip()
            if not line:
                continue

            parts = line.split()
            if len(parts) < 2:
                continue

            try:
                x = float(parts[0])
                y = float(parts[1])
            except ValueError:
                continue

            # Fortran separator between iterations
            if mode == "fortran" and x == 5000.0 and y == 5000.0:
                flush()
                continue

            # C# marker: start of new iteration
            if mode == "csharp" and x == 0.0 and y == 0.0:
                flush()
                continue

            current.append((x, y))

    flush()
    return iterations


def _plot_iterations_to_jpg(
    iterations: list[list[tuple[float, float]]],
    *,
    title: str,
    xlabel: str,
    ylabel: str,
    out_path: Path,
) -> None:
    """
    Plot all iterations on the same axes and save as JPG.
    """
    if not iterations:
        raise RuntimeError(f"No iterations to plot for {out_path.name}")

    plt.figure()
    for it in iterations:
        if not it:
            continue
        xs = [p[0] for p in it]
        ys = [p[1] for p in it]
        plt.plot(xs, ys)

    plt.title(title)
    plt.xlabel(xlabel)
    plt.ylabel(ylabel)
    plt.grid(True)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    plt.savefig(out_path, dpi=200, bbox_inches="tight")
    plt.close()


def render_run_plots(run_dir: Path, run_name: str) -> None:
    """
    For a given run_N directory:
      - reads fort.17 / fort.30 (Fortran) if present
      - reads plot_17.txt / plot_30.txt (C#) if present
    and writes JPG plots with required names.
    """
    # Axis info from docs:
    xlabel = "Time"
    ylabel = "Transmembrane voltage Vn"

    # Fortran plots
    f17 = run_dir / "fort.17"
    if f17.exists():
        iters = _parse_xy_file_iterations(f17, mode="fortran")
        _plot_iterations_to_jpg(
            iters,
            title=f"{run_name} fort.17 (Fortran)",
            xlabel=xlabel,
            ylabel=ylabel,
            out_path=run_dir / f"{run_name.replace('_','-')}-plot-17-fortran.jpg",
        )

    f30 = run_dir / "fort.30"
    if f30.exists():
        iters = _parse_xy_file_iterations(f30, mode="fortran")
        _plot_iterations_to_jpg(
            iters,
            title=f"{run_name} fort.30 (Fortran)",
            xlabel=xlabel,
            ylabel=ylabel,
            out_path=run_dir / f"{run_name.replace('_','-')}-plot-30-fortran.jpg",
        )

    # C# plots
    c17 = run_dir / "plot_17.txt"
    if c17.exists():
        iters = _parse_xy_file_iterations(c17, mode="csharp")
        _plot_iterations_to_jpg(
            iters,
            title=f"{run_name} plot_17.txt (C#)",
            xlabel=xlabel,
            ylabel=ylabel,
            out_path=run_dir / f"{run_name.replace('_','-')}-plot-17-csharp.jpg",
        )

    c30 = run_dir / "plot_30.txt"
    if c30.exists():
        iters = _parse_xy_file_iterations(c30, mode="csharp")
        _plot_iterations_to_jpg(
            iters,
            title=f"{run_name} plot_30.txt (C#)",
            xlabel=xlabel,
            ylabel=ylabel,
            out_path=run_dir / f"{run_name.replace('_','-')}-plot-30-csharp.jpg",
        )



def main() -> int:
    ap = argparse.ArgumentParser(description="Batch runs Fortran and C# SENN across results/run_N inparams sets.")
    ap.add_argument("--testing-dir", default=".", help="Path to the 'testing' folder (default: current dir)")
    ap.add_argument("--start", type=int, default=1, help="First run index (default: 1)")
    ap.add_argument("--end", type=int, default=50, help="Last run index inclusive (default: 50)")
    ap.add_argument("--fortran-exe", default=FORTRAN_DEFAULT, help=f"Fortran exe name (default: {FORTRAN_DEFAULT})")
    ap.add_argument("--csharp-exe", default=CSHARP_DEFAULT, help=f"C# exe name (default: {CSHARP_DEFAULT})")

    # Fortran completion heuristic tuning
    ap.add_argument("--poll-interval", type=float, default=0.25, help="Poll interval for output changes (seconds)")
    ap.add_argument("--stable-seconds", type=float, default=1.0, help="How long outputs must be unchanged (seconds)")
    ap.add_argument("--fortran-max-wait", type=float, default=300.0, help="Max wait for Fortran outputs (seconds)")
    ap.add_argument("--fortran-terminate-wait", type=float, default=5.0, help="Wait after terminate before kill")

    # C# timeout
    ap.add_argument("--csharp-timeout", type=int, default=0, help="Timeout seconds for C# (0 = no timeout)")
    ap.add_argument("--fortran-timeout", type=int, default=0, help="Timeout seconds for Fortran (0 = no timeout)")

    ap.add_argument("--continue-on-error", action="store_true", help="Continue after errors (default: stop)")
    args = ap.parse_args()

    testing_dir = Path(args.testing_dir).resolve()

    fortran_exe = testing_dir / args.fortran_exe
    csharp_exe = testing_dir / args.csharp_exe
    testing_inparams = testing_dir / "inparam.txt"

    csharp_timeout = None if args.csharp_timeout == 0 else args.csharp_timeout

    print(f"Testing dir : {testing_dir}")
    print(f"Runs        : {args.start}..{args.end}")
    print()

    failures: list[tuple[int, str]] = []

    for n in range(args.start, args.end + 1):
        run_name = f"run_{n}"
        run_dir = testing_dir / run_name
        run_inparams = run_dir / "inparam.txt"

        if run_dir == testing_dir:
            raise RuntimeError("Refusing to use testing directory as run directory")

        print(f"[{run_name}] starting...")

        try:
            if not run_inparams.exists():
                raise RuntimeError(f"Missing {run_inparams}")

            # Put correct inparams into testing folder
            shutil.copy2(run_inparams, testing_inparams)

            # --- Fortran ---
            run_fortran(
                fortran_exe,
                testing_dir,
                run_name,
                timeout=args.fortran_timeout if args.fortran_timeout != 0 else None,
            )
            # Copy fort.* into results/run_N
            copy_files(testing_dir, run_dir, FORTRAN_OUTPUTS)

            run_csharp(
                csharp_exe,
                testing_dir,
                run_dir,          # <-- outputs go directly into results/run_N
                run_name,
                timeout=csharp_timeout
            )
            render_run_plots(run_dir, run_name)
            print(f"[{run_name}] done.")

        except Exception as e:
            msg = str(e)
            print(f"[{run_name}] ERROR: {msg}", file=sys.stderr)
            failures.append((n, msg))
            if not args.continue_on_error:
                break

    print("\nSummary:")
    if not failures:
        print("  All runs completed successfully.")
        return 0
    else:
        print(f"  Failures: {len(failures)}")
        for n, msg in failures:
            print(f"   - run_{n}: {msg}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
