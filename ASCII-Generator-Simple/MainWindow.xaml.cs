using Microsoft.Win32;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.Drawing;
using WpfAnimatedGif;
using System.IO;

namespace ASCII_Generator_Simple
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // FIELDS
        string _path = "";
        readonly BitmapAscii asciify = new BitmapAscii();

        // CONSTRUCTOR
        public MainWindow()
        {
            InitializeComponent();

            // used for data binding
            this.DataContext = asciify;
        }

        #region WORKER METHODS

        private BackgroundWorker worker_Initialize()
        {
            // background worker initialization
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
            worker.DoWork += new DoWorkEventHandler(worker_DoWork);
            worker.ProgressChanged += new ProgressChangedEventHandler(worker_ProgressChanged);
            return worker;
        }

        private void worker_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e) {
            if (Path.GetExtension(_path) == ".gif") {
                var test = new BitmapImage();
                test.BeginInit();
                test.UriSource = new Uri(asciify.GifPath);
                test.EndInit();
                asciify.AsciiImage = test;
                ImageBehavior.SetAnimatedSource(imgAscii, test);
            }
            else {
                imgAscii.Source = asciify.AsciiImage;
            }
        }

        private void worker_ProgressChanged(object? sender, ProgressChangedEventArgs e)
        {// this is called when ReportProgress is called
            progressBar.Value = e.ProgressPercentage;
        }

        private void worker_DoWork(object? sender, DoWorkEventArgs e)
        {// the work that will be done on a seperate thread

            if ((sender as BackgroundWorker) == null)
            {
                throw new Exception("Sender is null");
            }

            // pass the current worker to the asciify class
            asciify.Worker = sender as BackgroundWorker;

            // get height and width of the grid that the ascii art txtbox is in
            asciify.TxtBoxWidth = txtBoxColumn.ActualWidth;
            asciify.TxtBoxHeight = txtBoxRow.ActualHeight;

            if (Path.GetExtension(_path) == ".gif") {
                Image image = Image.FromFile(_path);
                asciify.AsciitizeGIF(image);
            } else {
                // bitmap of selected image
                Bitmap bmp = new Bitmap(_path);
                // Call asciitize method
                asciify.Asciitize(bmp);
            }
        }


        #endregion


        #region OBJECT METHODS

        // Open button click
        private void menuOpen_Click(object sender, RoutedEventArgs e)
        {// click open button, top left

            ImageBehavior.SetAnimatedSource(imgMain, null);
            ImageBehavior.SetAnimatedSource(imgAscii, null);

            OpenFileDialog ofd = new OpenFileDialog();

            ofd.Filter = "Image Files(*.BMP;*.JPG;*.GIF;*.PNG)|*.BMP;*.JPG;*.GIF;*.PNG|All files (*.*)|*.*";

            // if the user presses ok on dialog box
            if (ofd.ShowDialog() == true)
            {
                _path = ofd.FileName;

                // if image is a gif
                if (Path.GetExtension(_path) == ".gif") {
                    asciify.IsGif = true;
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.UriSource = new Uri(_path);
                    image.EndInit();
                    ImageBehavior.SetAnimatedSource(imgMain, image);
                }
                else {
                    asciify.IsGif = false;
                    Uri uri = new Uri(_path);
                    BitmapImage bmi = new BitmapImage(uri);
                    imgMain.Source = bmi;
                }


                // empty progress bar
                progressBar.Value = 0;
            }
        }

        private void menuSave_Click(object sender, RoutedEventArgs e) {

            if (imgAscii.Source == null) {
                throw new Exception("No image to save");
            }

            SaveFileDialog sfd = new SaveFileDialog();

            sfd.Title = "Save the ascii image";
            sfd.Filter = "JPeg Image|*.jpg|Bitmap Image|*.bmp|Gif Image|*.gif";

            if (sfd.ShowDialog() == true) {
                _path = sfd.FileName;

                if (asciify.IsGif) {
                    File.Copy(asciify.GifPath, _path);
                }
                else {
                    if (asciify.Bmp != null) {
                        asciify.Bmp.Save(_path);
                    }
                }

            }
        }

        // Generate button click
        private void btnGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (imgMain.Source != null)
            {
                // Display and reset progress bar
                progressBar.Visibility = Visibility.Visible;
                lblProgress.Visibility = Visibility.Visible;
                progressBar.Value = 0;

                // init worker and start asciify
                BackgroundWorker worker = worker_Initialize();
                worker.RunWorkerAsync();
            }
        }

        // Settings menu button click
        private void menuSettings_Click(object sender, RoutedEventArgs e)
        {// make the settings menu visible when you click
            settingsBox.Visibility = Visibility.Visible;
        }

        // Settings menu OK button click
        private void btnOk_Click(object sender, RoutedEventArgs e)
        {// make the settings menu invisible when you click ok
            settingsBox.Visibility = Visibility.Collapsed;
        }

        // The black grid around the settings menu
        private void settingsGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {// do the same as hitting ok
            btnOk_Click(sender, e);
        }

        // Preview text in textbox
        private void txtbox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {// stops the user from inputing anything except numbers
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        #endregion


    }
}
