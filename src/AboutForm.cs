namespace NowPlayingToast;

internal sealed class AboutForm : Form
{
    public AboutForm(string? updateNote)
    {
        Text = "About Now Playing Toast";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 200);
        Font = new Font("Segoe UI", 9f);

        Controls.Add(new Label
        {
            Text = "Now Playing Toast",
            Left = 16, Top = 16, AutoSize = true,
            Font = new Font("Segoe UI Semibold", 12f)
        });

        Controls.Add(new Label
        {
            Text = "Version " + UpdateCheck.GetDisplayVersion(),
            Left = 16, Top = 48, AutoSize = true
        });

        var link = new LinkLabel
        {
            Text = UpdateCheck.GitHubUrl,
            Left = 16, Top = 76, AutoSize = true
        };
        link.LinkClicked += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = UpdateCheck.GitHubUrl,
                    UseShellExecute = true
                });
            }
            catch { }
        };
        Controls.Add(link);

        if (!string.IsNullOrWhiteSpace(updateNote))
        {
            Controls.Add(new Label
            {
                Text = updateNote,
                Left = 16, Top = 110, Width = 380, Height = 40
            });
        }

        var ok = new Button { Text = "OK", Left = 310, Top = 160, Width = 90, DialogResult = DialogResult.OK };
        Controls.Add(ok);
        AcceptButton = ok;
    }
}
