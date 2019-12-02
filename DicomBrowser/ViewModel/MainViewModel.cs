using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace DicomBrowser.ViewModel
{
    /// <summary>
    /// This class contains properties that the main View can data bind to.
    /// <para>
    /// Use the <strong>mvvminpc</strong> snippet to add bindable properties to this ViewModel.
    /// </para>
    /// <para>
    /// You can also use Blend to data bind with the tool's support.
    /// </para>
    /// <para>
    /// See http://www.galasoft.ch/mvvm
    /// </para>
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private ushort portMove = 10104;
        private string selectedPatient;
        private ushort port;
        private string aec = "ARCHIWUM";
        private string _ip;
        private string _port;
        private string _aet;
        private string _state;
        private ObservableCollection<string> patientList;
        private ObservableCollection<string> imageList;
        private string selectedImage;
        private BitmapImage imageToShow;

        public ICommand Refresh => new RelayCommand(OnRefresh);
        public ICommand Connect => new RelayCommand(OnConnect);
        public ICommand GetImages => new RelayCommand(OnGetImages);
        public ICommand Show => new RelayCommand(OnShow);

        public MainViewModel()
        {
            Port = "10100";
            IP = "127.0.0.1";
            AET = "KLIENTL";
            State = string.Empty;
            PatientList = new ObservableCollection<string>();
            ImageList = new ObservableCollection<string>();
        }
        private void OnRefresh(object obj)
        {
            PatientList.Clear();
            // typ wyszukiwania (rozpoczynamy od pacjenta)
            gdcm.ERootType typ = gdcm.ERootType.ePatientRootType;

            // do jakiego poziomu wyszukujemy 
            gdcm.EQueryLevel poziom = gdcm.EQueryLevel.ePatient; // zobacz inne 

            // klucze (filtrowanie lub okreœlenie, które dane s¹ potrzebne)
            gdcm.KeyValuePairArrayType klucze = new gdcm.KeyValuePairArrayType();
            //gdcm.Tag tag = new gdcm.Tag(0x0010, 0x0010);
            gdcm.KeyValuePairType klucz1 = new gdcm.KeyValuePairType(new gdcm.Tag(0x0010, 0x0010), "*");
            klucze.Add(klucz1);
            klucze.Add(new gdcm.KeyValuePairType(new gdcm.Tag(0x0010, 0x0020), ""));

            // skonstruuj zapytanie
            gdcm.BaseRootQuery zapytanie = gdcm.CompositeNetworkFunctions.ConstructQuery(typ, poziom, klucze);

            // sprawdŸ, czy zapytanie spe³nia kryteria
            if (!zapytanie.ValidateQuery())
            {
                State = "B³êdne zapytanie";
                return;
            }

            // wykonaj zapytanie
            gdcm.DataSetArrayType wynik = new gdcm.DataSetArrayType();
            bool stan = gdcm.CompositeNetworkFunctions.CFind(IP, ushort.Parse(Port), zapytanie, wynik, AET, "ARCHIWUM");

            // sprawdŸ stan
            if (!stan)
            {
                State = "Nie dzia³a";
                return;
            }

            // poka¿ wyniki
            foreach (gdcm.DataSet x in wynik)
            {
                // jeden element pary klucz-wartoœæ
                gdcm.DataElement de = x.GetDataElement(new gdcm.Tag(0x0010, 0x0020)); // konkretnie 10,20 = PATIENT_ID

                // dostêp jako string
                gdcm.Value val = de.GetValue(); // pobierz wartoœæ dla wskazanego klucza...
                string str = val.toString();    // ...jako napis
                PatientList.Add(str);
            }
        }


        private void OnGetImages(object obj)
        {
            ImageList.Clear();
            // typ wyszukiwania (rozpoczynamy od pacjenta)
            gdcm.ERootType typ = gdcm.ERootType.ePatientRootType;

            // do jakiego poziomu wyszukujemy 
            gdcm.EQueryLevel poziom = gdcm.EQueryLevel.ePatient; // zobacz inne 

            // klucze (filtrowanie lub okreœlenie, które dane s¹ potrzebne)
            gdcm.KeyValuePairArrayType klucze = new gdcm.KeyValuePairArrayType();
            if (SelectedPatient == null)
            {
                State = "Wybierz pacjenta";
                return;
            }

            gdcm.KeyValuePairType klucz1 = new gdcm.KeyValuePairType(new gdcm.Tag(0x0010, 0x0020), SelectedPatient); // NIE WOLNO TU STOSOWAC *; tutaj PatientID="01"
            klucze.Add(klucz1);

            // skonstruuj zapytanie
            gdcm.BaseRootQuery zapytanie = gdcm.CompositeNetworkFunctions.ConstructQuery(typ, poziom, klucze, true);

            // sprawdŸ, czy zapytanie spe³nia kryteria
            if (!zapytanie.ValidateQuery())
            {
                State = "MOVE b³êdne zapytanie!";
                return;
            }

            // przygotuj katalog na wyniki
            String odebrane = System.IO.Path.Combine(".", "odebrane"); // podkatalog odebrane w bie¿¹cym katalogu
            if (!System.IO.Directory.Exists(odebrane)) // jeœli nie istnieje
                System.IO.Directory.CreateDirectory(odebrane); // utwórz go
            String dane = System.IO.Path.Combine(odebrane, System.IO.Path.GetRandomFileName()); // wygeneruj losow¹ nazwê podkatalogu
            System.IO.Directory.CreateDirectory(dane); // i go utwórz

            // wykonaj zapytanie - pobierz do katalogu jak w zmiennej 'dane'
            bool stan = gdcm.CompositeNetworkFunctions.CMove(IP, ushort.Parse(Port), zapytanie, portMove, AET, aec, dane);

            // sprawdŸ stan
            if (!stan)
            {
                State = "MOVE nie dzia³a!";
                return;
            }


            List<string> pliki = new List<string>(System.IO.Directory.EnumerateFiles(dane));
            foreach (String plik in pliki)
            {

                // MOVE + konwersja
                // przeczytaj pixele
                gdcm.PixmapReader reader = new gdcm.PixmapReader();
                reader.SetFileName(plik);
                if (!reader.Read())
                {
                    // najpewniej nie jest to obraz
                    continue;
                }

                // przekonwertuj na "znany format"
                gdcm.Bitmap bmjpeg2000 = pxmap2jpeg2000(reader.GetPixmap());
                // przekonwertuj na .NET bitmapê
                Bitmap[] X = gdcmBitmap2Bitmap(bmjpeg2000);
                // zapisz
                for (int i = 0; i < X.Length; i++)
                {
                    String name = String.Format("{0}_warstwa{1}.jpg", plik, i);
                    X[i].Save(name);
                    ImageList.Add(name);
                }
            }

        }


        private void OnConnect(object obj)
        {
            port = ushort.Parse(Port);

            bool state = gdcm.CompositeNetworkFunctions.CEcho(IP, port, AET, aec);
            if (state)
            {
                State = "PO£¥CZONO";
            }
            else
            {
                State = "NIE PO£¥CZONO";
            }
        }


        private void OnShow(object obj)
        {
            string str = System.Environment.CurrentDirectory;
            if (selectedImage == null)
            {
                State = "Wybierz obraz";
                return;
            }
            SelectedImage = SelectedImage.Remove(0, 1);
            string imagePath = str + SelectedImage;
            ImageToShow = new BitmapImage(new Uri(imagePath, UriKind.Absolute));
        }


        public BitmapImage ImageToShow
        {
            get
            {
                return imageToShow;
            }
            set
            {
                imageToShow = value;
                RaisePropertyChanged(() => this.ImageToShow);
            }
        }

        public string SelectedPatient
        {
            get
            {
                return selectedPatient;
            }
            set
            {
                selectedPatient = value;
            }
        }

        public ObservableCollection<string> PatientList
        {
            get
            {
                return patientList;
            }
            set
            {
                patientList = value;
                RaisePropertyChanged(() => this.PatientList);
            }
        }

        public string SelectedImage
        {
            get
            {
                return selectedImage;
            }
            set
            {
                selectedImage = value;
            }
        }

        public ObservableCollection<string> ImageList
        {
            get
            {
                return imageList;
            }
            set
            {
                imageList = value;
                RaisePropertyChanged(() => this.ImageList);
            }
        }

        public string IP
        {
            get
            {
                return _ip;
            }
            set
            {
                _ip = value;
            }
        }

        public string Port
        {
            get
            {
                return _port;
            }
            set
            {
                _port = value;
            }
        }

        public string AET
        {
            get
            {
                return _aet;
            }
            set
            {
                _aet = value;
            }
        }


        public string State
        {
            get
            {
                return _state;
            }
            set
            {
                _state = value;
                RaisePropertyChanged(() => this.State);
            }
        }

        // Konwertuj Bitmapê GDCM na Bitmapê .NET, przy czym:
        // 1. za³ó¿ kodowanie LE, monochromatyczne
        // 2. ka¿d¹ z bitmap przeskaluj do 0-255 korzystaj¹c z wartoœci maksymalnej
        public static Bitmap[] gdcmBitmap2Bitmap(gdcm.Bitmap bmjpeg2000)
        {
            // przekonwertuj teraz na bitmapê C#
            uint cols = bmjpeg2000.GetDimension(0);
            uint rows = bmjpeg2000.GetDimension(1);
            uint layers = bmjpeg2000.GetDimension(2);

            // wartoœæ zwracana - tyle obrazków, ile warstw
            Bitmap[] ret = new Bitmap[layers];


            // bufor
            byte[] bufor = new byte[bmjpeg2000.GetBufferLength()];
            if (!bmjpeg2000.GetBuffer(bufor))
                throw new Exception("b³¹d pobrania bufora");

            // w strumieniu na ka¿dy piksel 2 bajty; tutaj LittleEndian (mnie znacz¹cy bajt wczeœniej)
            for (uint l = 0; l < layers; l++)
            {
                Bitmap X = new Bitmap((int)cols, (int)rows);
                double[,] Y = new double[cols, rows];
                double m = 0;

                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        // wspó³rzêdne w strumieniu
                        int j = ((int)(l * rows * cols) + (int)(r * cols) + c) * 2;
                        Y[r, c] = (double)bufor[j + 1] * 256 + bufor[j];
                        // przeskalujemy potem do wartoœci max.
                        if (Y[r, c] > m)
                            m = Y[r, c];
                    }

                // wolniejsza metoda tworzenia bitmapy
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        int f = (int)(255 * (Y[r, c] / m));
                        X.SetPixel(c, r, Color.FromArgb(f, f, f));
                    }
                // kolejna bitmapa
                ret[l] = X;
            }
            return ret;
        }


        // przekonwertuj do formatu bezstratnego JPEG2000
        // na podstawie: http://gdcm.sourceforge.net/html/StandardizeFiles_8cs-example.html
        public static gdcm.Bitmap pxmap2jpeg2000(gdcm.Pixmap px)
        {
            gdcm.ImageChangeTransferSyntax change = new gdcm.ImageChangeTransferSyntax();
            change.SetForce(false);
            change.SetCompressIconImage(false);
            change.SetTransferSyntax(new gdcm.TransferSyntax(gdcm.TransferSyntax.TSType.JPEG2000Lossless));

            change.SetInput(px);
            if (!change.Change())
                throw new Exception("Nie przekonwertowano typu bitmapy na jpeg2000");

            gdcm.Bitmap outimg = change.GetOutputAsBitmap(); // dla GDCM.3.0.4

            return outimg; //change.GetOutput(); // tak by³o w starszych wersjach
        }

    }
}