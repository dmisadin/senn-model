using SENNModel.Models.Enums;
using SENNModel.Models.IO;
using System;

namespace SENNModel.Models.Simulations;

public class McIntyreRichardsonGrillSimulation : BaseSimulation, ISimulation
{
    public McIntyreRichardsonGrillSimulation(FileExporter fileExporter) : base(fileExporter)
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

    // McIntyre-Richardson-Grill model implementation (human peripheral nerve fibers)
    private void ComputeNonlinearNodes(SennState s)
    {
        // No nonlinear nodes
        if (s.NLIN1 <= 0 || s.NLIN1 > s.NLIN2)
            return;

        const double PERX2 = 0.0002;          // Small threshold for numerical stability
        int jt = 2 * s.NON + 1;               // Last linear node index

        // A(1..4), B(1..4) – rate coefficients for h_t, m_t, m_p, s
        double[] A = new double[5];  // A[1]=h_t, A[2]=m_t, A[3]=m_p, A[4]=s
        double[] B = new double[5];  // B[1]=h_t, B[2]=m_t, B[3]=m_p, B[4]=s

        int jCount = 0; // counts nonlinear nodes

        for (int k = s.NLIN1; k <= s.NLIN2; k++)
        {
            int L = jt + 4 * jCount; // base index for h_t,m_t,m_p,s at this node
            double V = s.Y[k];  // membrane potential at node k (mV)

            // ---------- m_t gate (transient sodium activation) ----------
            // MRG model: alpha_m_t(V) = 0.1*(V+40)/(1-exp(-(V+40)/10))
            //            beta_m_t(V) = 4*exp(-(V+65)/18)
            // Similar to HH but adapted for human peripheral nerve
            double V_shift_mt = V + 40.0;
            double delv_mt = PERX2 * 10.0;

            if (Math.Abs(V_shift_mt) > delv_mt)
            {
                double exp_arg = -V_shift_mt / 10.0;
                if (exp_arg > 87.0)
                {
                    A[2] = 1e-36;
                }
                else
                {
                    double denom = 1.0 - Math.Exp(exp_arg);
                    if (Math.Abs(denom) > 1e-10)
                        A[2] = 0.1 * V_shift_mt / denom;
                    else
                        A[2] = 0.1 * 10.0; // L'Hôpital limit
                }
            }
            else
            {
                A[2] = 0.1 * 10.0; // L'Hôpital limit
            }

            double exp_arg_beta_mt = -(V + 65.0) / 18.0;
            B[2] = (exp_arg_beta_mt < 87.0) ? 4.0 * Math.Exp(exp_arg_beta_mt) : 0.0;

            // ---------- h_t gate (transient sodium inactivation) ----------
            // MRG model: alpha_h_t(V) = 0.07*exp(-(V+65)/20)
            //            beta_h_t(V) = 1/(1+exp(-(V+35)/10))
            double exp_arg_alpha_ht = -(V + 65.0) / 20.0;
            A[1] = (exp_arg_alpha_ht < 87.0) ? 0.07 * Math.Exp(exp_arg_alpha_ht) : 0.0;

            double exp_arg_beta_ht = -(V + 35.0) / 10.0;
            if (exp_arg_beta_ht < 78.0)
                B[1] = 1.0 / (1.0 + Math.Exp(exp_arg_beta_ht));
            else
                B[1] = 0.0;

            // ---------- m_p gate (persistent sodium activation) ----------
            // MRG model: persistent sodium has slower kinetics
            // alpha_m_p(V) = 0.003*(V+40)/(1-exp(-(V+40)/10))
            // beta_m_p(V) = 0.003*exp(-(V+65)/18)
            double V_shift_mp = V + 40.0;
            double delv_mp = PERX2 * 10.0;

            if (Math.Abs(V_shift_mp) > delv_mp)
            {
                double exp_arg = -V_shift_mp / 10.0;
                if (exp_arg > 87.0)
                {
                    A[3] = 1e-36;
                }
                else
                {
                    double denom = 1.0 - Math.Exp(exp_arg);
                    if (Math.Abs(denom) > 1e-10)
                        A[3] = 0.003 * V_shift_mp / denom;
                    else
                        A[3] = 0.003 * 10.0; // L'Hôpital limit
                }
            }
            else
            {
                A[3] = 0.003 * 10.0; // L'Hôpital limit
            }

            double exp_arg_beta_mp = -(V + 65.0) / 18.0;
            B[3] = (exp_arg_beta_mp < 87.0) ? 0.003 * Math.Exp(exp_arg_beta_mp) : 0.0;

            // ---------- s gate (slow potassium activation) ----------
            // MRG model: alpha_s(V) = 0.3/(1+exp(-(V+27)/10))
            //            beta_s(V) = 0.03*exp(-(V+27)/80)
            double exp_arg_alpha_s = -(V + 27.0) / 10.0;
            if (exp_arg_alpha_s < 78.0)
                A[4] = 0.3 / (1.0 + Math.Exp(exp_arg_alpha_s));
            else
                A[4] = 0.0;

            double exp_arg_beta_s = -(V + 27.0) / 80.0;
            B[4] = (exp_arg_beta_s < 87.0) ? 0.03 * Math.Exp(exp_arg_beta_s) : 0.0;

            // ---------- gating derivatives dh_t/dt, dm_t/dt, dm_p/dt, ds/dt ----------
            // Map to FH structure: L=h_t, L+1=m_t, L+2=m_p, L+3=s
            double h_t = s.Y[L];
            double m_t = s.Y[L + 1];
            double m_p = s.Y[L + 2];
            double s_var = s.Y[L + 3];

            s.DERY[L] = A[1] * (1.0 - h_t) - B[1] * h_t;        // dh_t/dt
            s.DERY[L + 1] = A[2] * (1.0 - m_t) - B[2] * m_t;    // dm_t/dt
            s.DERY[L + 2] = A[3] * (1.0 - m_p) - B[3] * m_p;    // dm_p/dt
            s.DERY[L + 3] = A[4] * (1.0 - s_var) - B[4] * s_var; // ds/dt

            // ---------- ionic currents at nonlinear node k ----------
            // MRG model: I_NaT = g_NaT * m_t^3 * h_t * (V - E_Na)
            //            I_NaP = g_NaP * m_p^3 * (V - E_Na)
            //            I_Ks = g_Ks * s^4 * (V - E_K)
            //            I_L = g_L * (V - E_L)

            // MRG conductances (mS/cm²) - human peripheral nerve values
            // Scale to match FH current scale
            const double SCALE_FACTOR = 0.001;  // Scale factor to match FH current magnitude
            const double G_NAT_MAX = 3.0 * SCALE_FACTOR;   // Transient Na (S/cm² -> scaled)
            const double G_NAP_MAX = 0.01 * SCALE_FACTOR; // Persistent Na (S/cm² -> scaled)
            const double G_KS_MAX = 0.08 * SCALE_FACTOR;  // Slow K (S/cm² -> scaled)
            const double G_L_MRG = 0.007 * SCALE_FACTOR;  // Leak conductance

            // Reversal potentials (mV) - typical for human peripheral nerve
            const double E_NA_MRG = 50.0;     // mV
            const double E_K_MRG = -90.0;     // mV
            const double E_L_MRG = -90.0;     // mV

            // Calculate conductances (scaled mS/cm²)
            double g_nat = G_NAT_MAX * Math.Pow(m_t, 3.0) * h_t;
            double g_nap = G_NAP_MAX * Math.Pow(m_p, 3.0);
            double g_ks = G_KS_MAX * Math.Pow(s_var, 4.0);
            double g_l = G_L_MRG;

            // Calculate currents: g(mS/cm²) * (V-E)(mV) = μA/cm²
            // No *1000.0 multiplier (same as HH/CRRS fix)
            s.UINA[k] = g_nat * (V - E_NA_MRG);  // Transient sodium
            s.UIP[k] = g_nap * (V - E_NA_MRG);   // Persistent sodium (stored in UIP)
            s.UIK[k] = g_ks * (V - E_K_MRG);     // Slow potassium
            s.UIL[k] = g_l * (V - E_L_MRG);      // Leak

            // Total ionic current
            s.SUMK[k] = s.UINA[k] + s.UIP[k] + s.UIK[k] + s.UIL[k];

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
