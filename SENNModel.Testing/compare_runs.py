#!/usr/bin/env python3
import argparse
import shutil
import subprocess
import sys
import time
from dataclasses import dataclass
from pathlib import Path


FORTRAN_DEFAULT = "SENN.exe"
CSHARP_DEFAULT = "SENNModel.exe"

FORTRAN_OUTPUTS = ["fort.17", "fort.30"]
CSHARP_OUTPUTS = ["plot_17.txt", "plot_30.txt"]


@dataclass(frozen=True)
class FileState:
    exists: bool
    size: int
    mtime: float


def _state(p: Path) -> FileState:
    if not p.exists():
        return FileState(False, 0, 0.0)
    st = p.stat()
    return FileState(True, st.st_size, st.st_mtime)


def wait_for_outputs_stable(
    workdir: Path,
    filenames: list[str],
    *,
    poll_interval: float,
    stable_seconds: float,
    max_wait_seconds: float,
) -> None:
    """
    Wait until all files exist AND their (size, mtime) stop changing for stable_seconds.
    Raises TimeoutError if max_wait_seconds exceeded.
    """
    start = time.time()
    last_change = None
    last_states: dict[str, FileState] = {}

    while True:
        now = time.time()
        if now - start > max_wait_seconds:
            raise TimeoutError(
                f"Timed out waiting for outputs to stabilize: {filenames} "
                f"(waited {max_wait_seconds}s)."
            )

        states = {fn: _state(workdir / fn) for fn in filenames}
        all_exist = all(st.exists for st in states.values())

        # Track changes once files exist
        changed = False
        if all_exist:
            if not last_states:
                changed = True
            else:
                for fn, st in states.items():
                    prev = last_states.get(fn)
                    if prev is None or (st.size != prev.size) or (st.mtime != prev.mtime):
                        changed = True
                        break

            if changed:
                last_change = now
            else:
                # unchanged; see if stable long enough
                if last_change is not None and (now - last_change) >= stable_seconds:
                    return

        last_states = states
        time.sleep(poll_interval)


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


def run_fortran_with_output_detection(
    exe: Path,
    workdir: Path,
    run_name: str,
    *,
    poll_interval: float,
    stable_seconds: float,
    max_wait_seconds: float,
    graceful_terminate_seconds: float,
) -> None:
    """
    Start Fortran exe; if it exits, great.
    If it does not exit, decide 'done' when fort.* outputs stabilize, then terminate.
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
        proc = subprocess.Popen(
            [str(exe)],
            cwd=str(workdir),
            stdout=out,
            stderr=err,
        )

        # First: try waiting a little bit for natural exit while still monitoring outputs
        # We'll use output stabilization as the primary completion signal.
        try:
            wait_for_outputs_stable(
                workdir,
                FORTRAN_OUTPUTS,
                poll_interval=poll_interval,
                stable_seconds=stable_seconds,
                max_wait_seconds=max_wait_seconds,
            )
        except Exception:
            # If something went wrong, also check if process already exited
            ret = proc.poll()
            if ret is not None and ret != 0:
                raise RuntimeError(
                    f"Fortran process exited early with code {ret}. "
                    f"See {stdout_path.name} / {stderr_path.name}"
                )
            raise

        # At this point, outputs are stable -> consider the run finished.
        # If process is still running, terminate it.
        ret = proc.poll()
        if ret is None:
            proc.terminate()
            try:
                proc.wait(timeout=graceful_terminate_seconds)
            except subprocess.TimeoutExpired:
                proc.kill()
                proc.wait()

        else:
            # Process exited; non-zero is a failure
            if ret != 0:
                raise RuntimeError(
                    f"Fortran run failed with exit code {ret}. "
                    f"See {stdout_path.name} / {stderr_path.name}"
                )


def copy_files(src_dir: Path, dst_dir: Path, filenames: list[str]) -> None:
    dst_dir.mkdir(parents=True, exist_ok=True)
    for fn in filenames:
        src = src_dir / fn
        if not src.exists():
            raise RuntimeError(f"Expected output missing: {src}")
        shutil.copy2(src, dst_dir / fn)


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

    ap.add_argument("--continue-on-error", action="store_true", help="Continue after errors (default: stop)")
    args = ap.parse_args()

    testing_dir = Path(args.testing_dir).resolve()

    fortran_exe = testing_dir / args.fortran_exe
    csharp_exe = testing_dir / args.csharp_exe
    testing_inparams = testing_dir / "inparams.txt"

    csharp_timeout = None if args.csharp_timeout == 0 else args.csharp_timeout

    print(f"Testing dir : {testing_dir}")
    print(f"Runs        : {args.start}..{args.end}")
    print()

    failures: list[tuple[int, str]] = []

    for n in range(args.start, args.end + 1):
        run_name = f"run_{n}"
        run_dir = testing_dir / run_name
        run_inparams = run_dir / "inparams.txt"

        if run_dir == testing_dir:
            raise RuntimeError("Refusing to use testing directory as run directory")

        print(f"[{run_name}] starting...")

        try:
            if not run_inparams.exists():
                raise RuntimeError(f"Missing {run_inparams}")

            # Put correct inparams into testing folder
            shutil.copy2(run_inparams, testing_inparams)

            # --- Fortran ---
            run_fortran_with_output_detection(
                fortran_exe,
                testing_dir,
                run_name,
                poll_interval=args.poll_interval,
                stable_seconds=args.stable_seconds,
                max_wait_seconds=args.fortran_max_wait,
                graceful_terminate_seconds=args.fortran_terminate_wait,
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
