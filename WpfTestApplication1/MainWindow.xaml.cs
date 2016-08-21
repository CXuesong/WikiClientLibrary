using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
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

namespace WpfTestApplication1
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private WikiClient client;
        private Site site;
        private Dictionary<string, ParsedContentInfo> parsedPages = new Dictionary<string, ParsedContentInfo>();
        private CancellationTokenSource LastNavigationCancellation;

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
            SetStatus("Navigating to: " + title);
            ParsedContentInfo parsed;
            try
            {
                parsed = await site.ParsePage(title, true);
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
            Action<string, string> fillParam = (name, value) => text = text.Replace("<!-- " + name + " -->", value);
            fillParam("SITE NAME", site.SiteInfo.SiteName);
            fillParam("DISPLAY TITLE", parsed.DisplayTitle);
            fillParam("CONTENT", parsed.Text);
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
                EndPointUrl = EndPointUrl,
                ClientUserAgent = "ApplicationTest/1.0 (.NET CLR " + Environment.Version + ")",
            };
            SetStatus("Loading wiki site info: " + client.EndPointUrl);
            site = await Site.GetAsync(client);
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
                    var anchor = doc.getElementById(section.Anchor);
                    anchor.ScrollIntoView(true);
                }
            }
        }
    }

    class AbortionIndicator
    {
        public bool Aborted { get; set; }
    }
}
