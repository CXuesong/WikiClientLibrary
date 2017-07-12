using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using WikiClientLibrary;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;

namespace WpfTestApplication1
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private WikiClient client;
        private Site site;
        private CancellationTokenSource LastNavigationCancellation;
        private Regex articleUrlMatcher = null;

        public const string EndPointUrl = "https://en.wikipedia.org/w/api.php";

        private string PageTemplate;

        public MainWindow()
        {
            InitializeComponent();
            using (var s = typeof (MainWindow).Assembly.GetManifestResourceStream("WpfTestApplication1.WikiPageTemplate.html"))
            {
                if (s == null) throw new MissingManifestResourceException("Wiki page template file is missing.");
                using (var reader = new StreamReader(s))
                    PageTemplate = reader.ReadToEnd();
            }
        }

        private void SetStatus(string status = null)
        {
            StatusLabel.Content = status;
        }

        private async Task NavigateCoreAsync(string title, CancellationToken token)
        {
            TitleTextBox.Text = title;
            SetStatus("Navigating to: " + title);
            ParsedContentInfo parsed;
            try
            {
                parsed = await site.ParsePageAsync(title, ParsingOptions.DisableToc, token);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                SetStatus(ex.Message);
                return;
            }
            if (token.IsCancellationRequested) goto CLEANUP;
            SetStatus("Parsing: " + parsed.Title);
            TitleTextBox.Text = parsed.Title;
            // Fill the page.
            var text = PageTemplate;
            void FillParam(string name, string value) => text = text.Replace("<!-- " + name + " -->", value);
            FillParam("SITE NAME", site.SiteInfo.SiteName);
            FillParam("DISPLAY TITLE", parsed.DisplayTitle);
            FillParam("CONTENT", parsed.Content);
            if (token.IsCancellationRequested) goto CLEANUP;
            PageFrame.NavigateToString(text);
            if (token.IsCancellationRequested) goto CLEANUP;
            // Fill TOC.
            TocListBox.Items.Clear();
            foreach (var s in parsed.Sections)
            {
                TocListBox.Items.Add(s);
            }
            CLEANUP:
            SetStatus();
        }

        private void Navigate(string title)
        {
            LastNavigationCancellation?.Cancel();
            LastNavigationCancellation = new CancellationTokenSource();
            var task = NavigateCoreAsync(title, LastNavigationCancellation.Token);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Play with some real wiki.
            client = new WikiClient
            {
                ClientUserAgent = "WpfApplicationTest/1.0 (.NET CLR " + Environment.Version + ")",
            };
            SetStatus("Loading wiki site info: " + EndPointUrl);
            site = await Site.CreateAsync(client, EndPointUrl);
            articleUrlMatcher = new Regex(Regex.Escape(site.SiteInfo.ArticlePath).Replace(@"\$1", "(.+?)") + "$");
            Navigate(site.SiteInfo.MainPage);
        }

        private void PageFrame_LoadCompleted(object sender, NavigationEventArgs e)
        {

        }

        private void TitleTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Navigate(TitleTextBox.Text);
            }
        }

        private void TocListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems != null && e.AddedItems.Count > 0)
            {
                var section = e.AddedItems[0] as ContentSectionInfo;
                if (section != null)
                {
                    dynamic doc = PageFrame.Document;
                    var anchor = doc?.getElementById(section.Anchor);
                    if (anchor != null && !Convert.IsDBNull(anchor))
                        anchor.ScrollIntoView(true);
                }
            }
        }

        private void PageFrame_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            if (e.Uri != null)
            {
                // Actual navigation is to take place.
                TocListBox.Items.Clear();
                var titleMatch = articleUrlMatcher.Match(e.Uri.ToString());
                if (titleMatch.Success)
                {
                    e.Cancel = true;
                    Navigate(WebUtility.UrlDecode(titleMatch.Groups[1].Value));
                }
            }
        }
    }

    class AbortionIndicator
    {
        public bool Aborted { get; set; }
    }
}
