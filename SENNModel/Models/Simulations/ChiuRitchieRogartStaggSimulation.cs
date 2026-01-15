using SENNModel.Models.Enums;
using SENNModel.Models.IO;
using System;

namespace SENNModel.Models.Simulations;

public class ChiuRitchieRogartStaggSimulation :  BaseSimulation, ISimulation
{
    public ChiuRitchieRogartStaggSimulation(FileExporter fileExporter) : base(fileExporter)
    {
    }

    public RunNextAction? ExecuteSimulationStep(SennState state)
    {
        throw new System.NotImplementedException();
    }

    protected override void FCT(double x, SennState s)
    {
        throw new NotImplementedException();
    }

    protected override void OutputStep(double x, SennState s, int iHLF, int nDim)
    {
        throw new NotImplementedException();
    }


    // Chiu-Ritchie-Rogart-Stagg model implementation (mammalian myelinated axons)
    private void ComputeNonlinearNodes(SennState s)
    {
        // No nonlinear nodes
        if (s.NLIN1 <= 0 || s.NLIN1 > s.NLIN2)
            return;

        const double PERX2 = 0.0002;          // Small threshold for numerical stability
        int jt = 2 * s.NON + 1;               // Last linear node index

        // A(1..3), B(1..3) – rate coefficients for h, m, n
        double[] A = new double[4];  // A[1]=h, A[2]=m, A[3]=n
        double[] B = new double[4];  // B[1]=h, B[2]=m, B[3]=n

        int jCount = 0; // counts nonlinear nodes

        for (int k = s.NLIN1; k <= s.NLIN2; k++)
        {
            int L = jt + 4 * jCount; // base index for h,m,n at this node (using 4 slots for compatibility)
            double V = s.Y[k];  // membrane potential at node k (mV)

            // ---------- m gate (sodium activation) ----------
            // CRRS model rate constants for mammalian myelinated axons
            // alpha_m(V) = 0.36*(V+33)/(1-exp(-(V+33)/3))
            // beta_m(V) = 0.4*exp(-(V+60)/20)
            double V_shift_m = V + 33.0;
            double delv_m = PERX2 * 3.0;

            if (Math.Abs(V_shift_m) > delv_m)
            {
                double exp_arg = -V_shift_m / 3.0;
                if (exp_arg > 87.0)
                {
                    A[2] = 1e-36;
                }
                else
                {
                    double denom = 1.0 - Math.Exp(exp_arg);
                    if (Math.Abs(denom) > 1e-10)
                        A[2] = 0.36 * V_shift_m / denom;
                    else
                        A[2] = 0.36 * 3.0; // L'Hôpital limit
                }
            }
            else
            {
                A[2] = 0.36 * 3.0; // L'Hôpital limit
            }

            double exp_arg_beta_m = -(V + 60.0) / 20.0;
            B[2] = (exp_arg_beta_m < 87.0) ? 0.4 * Math.Exp(exp_arg_beta_m) : 0.0;

            // ---------- h gate (sodium inactivation) ----------
            // CRRS model: alpha_h(V) = 0.1*exp(-(V+60)/20)
            //            beta_h(V) = 4.5/(1+exp(-(V+30)/10))
            double exp_arg_alpha_h = -(V + 60.0) / 20.0;
            A[1] = (exp_arg_alpha_h < 87.0) ? 0.1 * Math.Exp(exp_arg_alpha_h) : 0.0;

            double exp_arg_beta_h = -(V + 30.0) / 10.0;
            if (exp_arg_beta_h < 78.0)
                B[1] = 4.5 / (1.0 + Math.Exp(exp_arg_beta_h));
            else
                B[1] = 0.0;

            // ---------- n gate (potassium activation) ----------
            // CRRS model: alpha_n(V) = 0.02*(V+40)/(1-exp(-(V+40)/10))
            //            beta_n(V) = 0.05*exp(-(V+50)/80)
            double V_shift_n = V + 40.0;
            double delv_n = PERX2 * 10.0;

            if (Math.Abs(V_shift_n) > delv_n)
            {
                double exp_arg = -V_shift_n / 10.0;
                if (exp_arg > 87.0)
                {
                    A[3] = 1e-36;
                }
                else
                {
                    double denom = 1.0 - Math.Exp(exp_arg);
                    if (Math.Abs(denom) > 1e-10)
                        A[3] = 0.02 * V_shift_n / denom;
                    else
                        A[3] = 0.02 * 10.0; // L'Hôpital limit
                }
            }
            else
            {
                A[3] = 0.02 * 10.0; // L'Hôpital limit
            }

            double exp_arg_beta_n = -(V + 50.0) / 80.0;
            B[3] = (exp_arg_beta_n < 87.0) ? 0.05 * Math.Exp(exp_arg_beta_n) : 0.0;

            // ---------- gating derivatives dh/dt, dm/dt, dn/dt ----------
            // Store in same positions as FH: L=h, L+1=m, L+2=unused, L+3=n
            double h = s.Y[L];
            double m = s.Y[L + 1];
            double n = s.Y[L + 3];  // Use L+3 slot for n (L+2 is unused for CRRS)

            s.DERY[L] = A[1] * (1.0 - h) - B[1] * h;      // dh/dt
            s.DERY[L + 1] = A[2] * (1.0 - m) - B[2] * m;  // dm/dt
            s.DERY[L + 3] = A[3] * (1.0 - n) - B[3] * n;  // dn/dt
            // L+2 slot remains unused (no persistent sodium in CRRS)

            // ---------- ionic currents at nonlinear node k ----------
            // CRRS model: I_Na = g_Na_max * m^3 * h * (V - E_Na)
            //             I_K = g_K_max * n^4 * (V - E_K)
            //             I_L = g_L * (V - E_L)

            // CRRS conductances (mS/cm²) - mammalian myelinated axon values
            // Scale to match FH current scale (similar to HH)
            const double SCALE_FACTOR = 0.001;  // Scale factor to match FH current magnitude
            const double G_NA_MAX = 200.0 * SCALE_FACTOR;  // Higher than HH for mammalian
            const double G_K_MAX = 50.0 * SCALE_FACTOR;    // Higher than HH for mammalian
            const double G_L_CRRS = 0.5 * SCALE_FACTOR;    // Leak conductance

            // Reversal potentials (mV) - typical for mammalian
            const double E_NA_CRRS = 50.0;     // mV
            const double E_K_CRRS = -90.0;     // mV (more negative than HH)
            const double E_L_CRRS = -70.0;     // mV

            // Calculate conductances (scaled mS/cm²)
            double g_na = G_NA_MAX * Math.Pow(m, 3.0) * h;
            double g_k = G_K_MAX * Math.Pow(n, 4.0);
            double g_l = G_L_CRRS;

            // Calculate currents: g(mS/cm²) * (V-E)(mV) = μA/cm²
            // No *1000.0 multiplier (same as HH fix)
            s.UINA[k] = g_na * (V - E_NA_CRRS);
            s.UIK[k] = g_k * (V - E_K_CRRS);
            s.UIL[k] = g_l * (V - E_L_CRRS);
            s.UIP[k] = 0.0;  // No persistent sodium in CRRS model

            // Total ionic current
            s.SUMK[k] = s.UINA[k] + s.UIK[k] + s.UIL[k];

            // ---------- override dV/dt at nonlinear node ----------
            // Use same formula as FH model for consistency
            if (s.TT == 1)
            {
                // Truncated axon
                s.DERY[k] = (s.TIM[k] - s.SUMK[k] * s.AREA) / s.CCM;
            }
            else if (s.TT == 2)
            {
                // Cell body + hillock
                if (k == 2)
                    s.DERY[2] = (s.TIM[2] - s.SUMK[k] * s.AH) / s.CMH;
                else if (k == 3)
                    s.DERY[3] = (s.TIM[3] - s.SUMK[k] * s.AN) / s.CCM;
                else if (k > 3)
                    s.DERY[k] = (s.TIM[k] - s.SUMK[k] * s.AN) / s.CCM;
            }

            jCount++;
        }
    }
}
