using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

namespace SplitTextIntoStringarry
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        Dictionary<string, int> RESULT = new Dictionary<string, int>();
        List<string> PHRASELIST = new List<string>();
        List<FileInfo> FILELIST = new List<FileInfo>();
        public MainWindow()
        {
            InitializeComponent();
        }

        private void SplitBt_Click(object sender, RoutedEventArgs e)
        {
            SplitBt.IsEnabled = false;
            char[] SPLITOR = CharTB.Text.ToCharArray();
            FILELIST = GetAllFilesList(PathTb.Text, FILELIST);
            try
            {
                for (int i = 0; i < FILELIST.Count; i++)
                {
                    string bookcontent = Encoding.Default.GetString(ReadFileToStream(FILELIST[i].FullName));
                    string[] phrase = bookcontent.Split(SPLITOR);
                    for (int j = 0; j < phrase.Length; j++)
                    {
                        phrase[j] = phrase[j].Replace("\r\n", "");
                        if (phrase[j] != "")
                        {
                            if (!RESULT.ContainsKey(phrase[j])) RESULT.Add(phrase[j], 1); RESULT[phrase[j]]++;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                string filepath = System.Environment.CurrentDirectory + "\\result.txt";
                if (File.Exists(filepath)) File.Delete(filepath);
                FileStream TargetFS = new FileStream(filepath, FileMode.Append);
                StreamWriter sw = new StreamWriter(TargetFS);
                foreach (var item in RESULT)
                {
                    if (item.Value > Convert.ToInt16(rate.Text)) sw.WriteLine(item.Key + "@" + item.Value);
                }
                sw.Close();
                TargetFS.Close();
            }
        }

        private static List<FileInfo> GetAllFilesList(string RootDir, List<FileInfo> FileList)
        {
            DirectoryInfo DirInfo = new DirectoryInfo(RootDir);
            FileInfo[] FileInfo = DirInfo.GetFiles();
            DirectoryInfo[] SubDirInfo = DirInfo.GetDirectories();
            foreach (FileInfo fileinfo in FileInfo)
            {
                //int size = Convert.ToInt32(f.Length);
                FileList.Add(fileinfo); //添加文件路径到列表中
            }
            //获取子文件夹内的文件列表，递归遍历

            foreach (DirectoryInfo SubDir in SubDirInfo)
            {
                GetAllFilesList(SubDir.FullName, FileList);
            }
            //排序，非必要
            //FileList = FileList .OrderBy(T => T.FullName).ToList();
            return FileList;
        }

        private byte[] ReadFileToStream(string FilePath)
        {
            FileStream TargetFS = new FileStream(FilePath, FileMode.Open, FileAccess.Read);
            byte[] BufferBox = new byte[4096];
            int ReadSize = 0;
            using (MemoryStream MemStream = new MemoryStream())
            {
                MemStream.Seek(0, SeekOrigin.Begin);
                while ((ReadSize = TargetFS.Read(BufferBox, 0, BufferBox.Length)) > 0)
                {
                    MemStream.Write(BufferBox, 0, ReadSize);
                    Array.Clear(BufferBox, 0, BufferBox.Length);
                }
                byte[] ByteStream = MemStream.ToArray();
                MemStream.Close();
                TargetFS.Close();
                return ByteStream;
            }
        }

        private void WriteStreamToFile(string FilePath, byte[] ByteStream)
        {
            FileStream TargetFS = new FileStream(FilePath, FileMode.OpenOrCreate);
            TargetFS.Write(ByteStream, 0, ByteStream.Length);
            TargetFS.Close();
        }

        private void PathTb_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            PathTb.Text = "";
        }

    }
}
