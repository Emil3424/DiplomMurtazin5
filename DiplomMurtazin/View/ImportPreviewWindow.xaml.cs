using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace DiplomMurtazin.View
{
    public partial class ImportPreviewWindow : Window
    {
        public ImportPreviewData Data { get; set; }
        public ObservableCollection<ImportPreviewRow> PreviewRows { get; set; }
        public bool ImportConfirmed { get; private set; }

        public ImportPreviewWindow(ImportPreviewData data)
        {
            InitializeComponent();
            Data = data;
            PreviewRows = new ObservableCollection<ImportPreviewRow>(data.Rows);
            DataContext = this;
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            ImportConfirmed = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            ImportConfirmed = false;
            Close();
        }
    }

    public class ImportPreviewData
    {
        public string DocumentNumber { get; set; }
        public DateTime DocumentDate { get; set; }
        public string ReceiverName { get; set; }
        public string ReceiverAddress { get; set; }
        public string Basis { get; set; }
        public List<ImportPreviewRow> Rows { get; set; } = new List<ImportPreviewRow>();
    }

    public class ImportPreviewRow
    {
        public string ProductName { get; set; }
        public string Barcode { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string Status { get; set; }
        public Brush StatusColor { get; set; }
    }
}