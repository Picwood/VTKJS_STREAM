using CaeModel.CFD.BaramFlow;
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class BaramFlowRunFormProbe
{
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static T Field<T>(Type type, object instance, string name) where T : class
    {
        return type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance) as T;
    }

    private static void CheckBounds(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control.Visible && control.Left < 0)
                throw new Exception("Control starts outside its parent: " + control.Text);
            if (control.Visible && control.Right > parent.ClientSize.Width + 2 && !(parent is TabControl))
                throw new Exception("Control exceeds parent width: " + control.Text);
            CheckBounds(control);
        }
    }

    private static T FindControl<T>(Control parent) where T : Control
    {
        if (parent is T) return (T)parent;
        foreach (Control child in parent.Controls)
        {
            T result = FindControl<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    private static Form Create(Type formType, BaramFlowGeneralSettings general)
    {
        return (Form)Activator.CreateInstance(formType, new object[] { general, new BaramFlowRunSettings() });
    }

    [STAThread]
    public static int Main()
    {
        Application.EnableVisualStyles();
        Assembly assembly = Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PrePoMax.exe"));
        Type formType = assembly.GetType("PrePoMax.Forms.FrmBaramFlowRunConditions", true);
        BaramFlowGeneralSettings steady = new BaramFlowGeneralSettings();
        using (Form form = Create(formType, steady))
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-30000, -30000);
            form.Show();
            Application.DoEvents();
            Require(Field<NumericUpDown>(formType, form, "_numberOfIterations").Enabled,
                    "Steady iteration count must be enabled.");
            Require(!Field<ComboBox>(formType, form, "_timeSteppingMethod").Enabled,
                    "Steady time stepping must be disabled.");
            CheckBounds(form);
            using (Bitmap image = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BaramFlowRun-Steady.png"));
            }
            NumericUpDown maximumFiles = Field<NumericUpDown>(formType, form, "_maximumNumberOfDataFiles");
            Require(!maximumFiles.Enabled, "Disabled file retention must lock the maximum file count.");
            Field<CheckBox>(formType, form, "_retainOnlyMostRecentFiles").Checked = true;
            Require(maximumFiles.Enabled, "Enabled file retention must unlock the maximum file count.");
            FindControl<TabControl>(form).SelectedIndex = 1;
            Application.DoEvents();
            using (Bitmap image = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BaramFlowRun-Output.png"));
            }
        }

        BaramFlowGeneralSettings transient = new BaramFlowGeneralSettings();
        transient.TimeMode = BaramFlowTimeMode.Transient;
        using (Form form = Create(formType, transient))
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-30000, -30000);
            form.Show();
            Application.DoEvents();
            ComboBox method = Field<ComboBox>(formType, form, "_timeSteppingMethod");
            Require(method.Enabled, "Transient pressure-based stepping must be editable.");
            method.SelectedIndex = 1;
            Application.DoEvents();
            Require(!Field<NumericUpDown>(formType, form, "_timeStepSize").Enabled,
                    "Adaptive time-step size must be disabled.");
            Require(Field<NumericUpDown>(formType, form, "_maximumCourantNumber").Enabled,
                    "Adaptive Courant control must be enabled.");
            Require(Field<NumericUpDown>(formType, form, "_maximumDiffusionNumber").Enabled,
                    "Adaptive diffusion control must be enabled.");
            CheckBounds(form);
            using (Bitmap image = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BaramFlowRun-Adaptive.png"));
            }
        }

        BaramFlowGeneralSettings density = new BaramFlowGeneralSettings();
        density.SolverType = BaramFlowSolverType.DensityBased;
        density.FlowType = BaramFlowFlowType.Compressible;
        density.TimeMode = BaramFlowTimeMode.Transient;
        using (Form form = Create(formType, density))
        {
            Require(!Field<ComboBox>(formType, form, "_timeSteppingMethod").Enabled,
                    "Density-based time stepping must remain fixed.");
        }
        Console.WriteLine("BaramFlow Run Conditions form probe passed.");
        return 0;
    }
}
