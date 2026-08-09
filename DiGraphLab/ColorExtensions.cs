using System;
using System.Drawing;
using Microsoft.Msagl.Drawing;

namespace DiGraphLab
{
    public static class ColorExtensions
    {
        public static Microsoft.Msagl.Drawing.Color ToMsagl(this System.Drawing.Color c) =>
            new Microsoft.Msagl.Drawing.Color(c.R, c.G, c.B);

        public static System.Drawing.Color ToSystem(this Microsoft.Msagl.Drawing.Color c) =>
            System.Drawing.Color.FromArgb(c.R, c.G, c.B);

        public static Microsoft.Msagl.Drawing.Color FromHtmlToMsagl(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return new Microsoft.Msagl.Drawing.Color(0, 0, 0);
            try
            {
                var sc = System.Drawing.ColorTranslator.FromHtml(html);
                return sc.ToMsagl();
            }
            catch
            {
                return new Microsoft.Msagl.Drawing.Color(0, 0, 0);
            }
        }
    }
}
