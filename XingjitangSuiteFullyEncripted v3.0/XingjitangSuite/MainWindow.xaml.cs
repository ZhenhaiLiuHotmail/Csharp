using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Management;
using System.Windows.Media.Imaging;
using System.Xml.Serialization;

namespace XingjitangSuite
{
    public partial class MainWindow : Window
    {
        //////////////////////////////////////// <General>
        string SUITEWORKROOT = System.Environment.CurrentDirectory + "\\";
        string AppDataXingjitangFolder = System.Environment.GetEnvironmentVariable("APPDATA") + "\\Xingjitang";
        string AppDataThisappFolder = System.Environment.GetEnvironmentVariable("APPDATA") + "\\Xingjitang\\WHOdict";
        string CfgFile = System.Environment.GetEnvironmentVariable("APPDATA") + "\\Xingjitang\\WHOdict\\CONFIG.CFG";
        string LimitedInput = " 1234567890;~|!@#%^&*()_+-=,.．/、'][<>【】；：\"“”’‘《》，。！·￥…（）…？?:{\\}";
        byte[] MYIV = new byte[16];
        byte[] MYKEY = new byte[32];
        byte[] MyAppKey = new byte[32];
        string deadline = "";

        private static List<FileInfo> GetAllFileList(string RootDir, List<FileInfo> FileList)
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
                GetAllFileList(SubDir.FullName, FileList);
            }
            return FileList;
        }

        static byte[] EncryptStringToBytes_Aes(byte[] plainText, byte[] Key, byte[] IV)
        {
            // Check arguments.
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException("plainText");
            if (Key == null || Key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (IV == null || IV.Length <= 0)
                throw new ArgumentNullException("IV");
            byte[] encrypted;
            // Create an Aes object
            // with the specified key and IV.
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;
                aesAlg.Padding = PaddingMode.Zeros;

                // Create a decrytor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for encryption.
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        csEncrypt.Write(plainText, 0, plainText.Length);
                        csEncrypt.FlushFinalBlock();
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }

            // Return the encrypted bytes from the memory stream.
            return encrypted;
        }

        static byte[] DecryptStringFromBytes_Aes(byte[] cipherText, byte[] Key, byte[] IV)
        {
            // Check arguments.
            if (cipherText == null || cipherText.Length <= 0)
                throw new ArgumentNullException("cipherText");
            if (Key == null || Key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (IV == null || IV.Length <= 0)
                throw new ArgumentNullException("IV");

            // Declare the string used to hold
            // the decrypted text.
            // Create an Aes object
            // with the specified key and IV.
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;
                aesAlg.Padding = PaddingMode.Zeros;
                // Create a decrytor to perform the stream transform.
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for decryption.
                using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        byte[] buffer = new byte[cipherText.Length];
                        csDecrypt.Read(buffer, 0, buffer.Length);

                        int i = 0;
                        for (i = 0; i < 15; i++)
                        {
                            if (buffer[buffer.Length - 1 - i] != 0)
                            {
                                break;
                            }
                        }

                        if (i > 0)
                        {
                            buffer = buffer.Take(buffer.Length - i).ToArray();
                        }
                        return buffer;
                    }
                }
            }
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

        private void WriteContentToFile(string FilePath, string content)
        {
            FileStream newfs = new FileStream(FilePath, FileMode.OpenOrCreate);
            StreamWriter newsw = new StreamWriter(newfs, Encoding.UTF8);
            newsw.WriteLine(content);
            newsw.Close();
            newfs.Close();
            //MessageBox.Show("Writing finished!\n"+FilePath );
            //Process.Start(@"c:\windows\system32\notepad.exe", FilePath);
        }

        private string OpenFileReadContent(string FilePath)
        {
            string content = string.Empty;
            FileStream oldfs = new FileStream(FilePath, FileMode.Open, FileAccess.Read);
            StreamReader oldsr = new StreamReader(oldfs, Encoding.UTF8);
            content = oldsr.ReadToEnd();
            oldsr.Close();
            oldfs.Close();
            if (content != "")
            {
                return content;
            }
            else
            {
                MessageBox.Show("Null Content!");
                return content;
            }
        }

        private Paragraph HighlightKeyStrings(string FullContent, string[] KeyStringArray)
        {
            KeyStringArray = KeyStringArray.Where(s => !string.IsNullOrEmpty(s)).ToArray();//排空
            //KeyStringArray = KeyStringArray.OrderBy(s => s.Length).ToArray();
            Color[] KeyColorArray = { Color.FromRgb(128, 255, 128), Color.FromRgb(255, 128, 128), Color.FromRgb(128, 128, 255), Color.FromRgb(255, 255, 128), Color.FromRgb(128, 255, 255), Color.FromRgb(255, 128, 255), Color.FromRgb(200, 200, 200) };
            Paragraph ResultParapragh = new Paragraph(new Run(FullContent));
            int KeyColorNo = 0;
            for (int i = 0; i < KeyStringArray.Count(); i++)
            {
                if (KeyStringArray[i] != "")
                {
                    List<TextRange> KeyStringRanges = new List<TextRange>();
                    TextPointer Position = ResultParapragh.ContentStart;
                    while (Position != null)
                    {
                        if (Position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                        {
                            //带有内容的文本
                            string KeyStringRun = Position.GetTextInRun(LogicalDirection.Forward);

                            //查找关键字在这文本中的位置
                            int indexInRun = KeyStringRun.IndexOf(KeyStringArray[i]);
                            int indexHistory = 0;
                            while (indexInRun >= 0)
                            {
                                TextPointer start = Position.GetPositionAtOffset(indexInRun + indexHistory);
                                TextPointer end = start.GetPositionAtOffset(KeyStringArray[i].Length);
                                KeyStringRanges.Add(new TextRange(start, end));
                                indexHistory = indexHistory + indexInRun + KeyStringArray[i].Length;
                                KeyStringRun = KeyStringRun.Substring(indexInRun + KeyStringArray[i].Length);//去掉已经采集过的内容
                                indexInRun = KeyStringRun.IndexOf(KeyStringArray[i]);//重新判断新的字符串是否还有关键字
                            }
                        }
                        Position = Position.GetNextContextPosition(LogicalDirection.Forward);
                    }

                    foreach (var CurrentKeyStringRange in KeyStringRanges)
                    {
                        int j = i;
                        if (i >= KeyColorArray.Count()) j = KeyColorArray.Count() - 1;
                        CurrentKeyStringRange.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(KeyColorArray[KeyColorNo]));
                    }
                    KeyColorNo++;
                    if (KeyColorNo == KeyColorArray.Count()) KeyColorNo = 0;
                }
            }
            return ResultParapragh;
        }

        private byte[] ObjectSerialze(object obj)
        {
            MemoryStream stream = new MemoryStream();
            BinaryFormatter bf = new BinaryFormatter();
            bf.Serialize(stream, obj);
            byte[] newArray = new byte[stream.Length];
            stream.Position = 0;
            stream.Read(newArray, 0, (int)stream.Length);
            stream.Close();
            return newArray;
        }

        private Object ByteDeserialize(byte[] array)
        {
            MemoryStream stream = new MemoryStream(array);
            BinaryFormatter bf = new BinaryFormatter();
            Object obj = bf.Deserialize(stream);
            stream.Close();
            return obj;
        }

        private string[] EnvironmentSerialNumber()
        {
            string[] mgtkey = { "DiskDriveModelSerialNumber", "BaseboardSerialNumber" };
            ManagementObjectSearcher VolumeSearcher = new ManagementObjectSearcher("select * from  win32_diskdrive");
            foreach (ManagementObject mgt in VolumeSearcher.Get())
            {
                mgtkey[0] += mgt["Model"].ToString() + ":" + mgt["SerialNumber"] + "\r\n";
            }
            ManagementObjectSearcher BaseboardSearcher = new ManagementObjectSearcher("select * from  win32_baseboard");
            foreach (ManagementObject mgt in BaseboardSearcher.Get())
            {
                mgtkey[1] = mgt["SerialNumber"].ToString();
                break;
            }

            return mgtkey;
        }

        private void SelfDestroy()
        {
            List<FileInfo> alldata = new List<FileInfo>();
            alldata = GetAllFileList(SUITEWORKROOT + "xjt", alldata);
            foreach(FileInfo currentfile in alldata)
            {
                File.Delete(currentfile.FullName);
            }
            alldata.Clear();

            alldata = GetAllFileList(SUITEWORKROOT + "pic", alldata);
            foreach (FileInfo currentfile in alldata)
            {
                File.Delete(currentfile.FullName);
            }
            alldata.Clear();
            
        }

        private bool CheckbytesEquals(byte[] b1, byte[] b2)
        {
            if (b1.Length != b2.Length) return false;
            if (b1 == null || b2 == null) return false;
            for (int i = 0; i < b1.Length; i++)
                if (b1[i] != b2[i])
                    return false;
            return true;
        }

        private void ValidationCheck()
        {
            if (!File.Exists(SUITEWORKROOT + "xjt\\0.xjt"))
            {
                MessageBox.Show("授权文件不存在！");
                SelfDestroy();
                System.Environment.Exit(0);
            }

            string[] LicenceFileInfo = (String[])ByteDeserialize(DecryptStringFromBytes_Aes(ReadFileToStream(SUITEWORKROOT+ "xjt\\0.xjt"), MYKEY, MYIV));
            deadline = LicenceFileInfo[2];

            if (!EnvironmentSerialNumber()[0].Contains(LicenceFileInfo[0]))
            {
                MessageBox.Show("授权设备不存在！");
                SelfDestroy();
                System.Environment.Exit(0);
            }

            string today = System.DateTime.Now.ToString("s").Replace("-", "").Substring(0,8);
            if (Convert.ToInt64(today) > Convert.ToInt64(LicenceFileInfo[2]))
            {
                SelfDestroy();
                MessageBox.Show("授权已过期！");
                System.Environment.Exit(0);
            }

            MyAppKey = EncryptStringToBytes_Aes(Encoding.UTF8.GetBytes(LicenceFileInfo[0]), MYKEY, MYIV);
            //RegistryKey RegRead;
            //RegRead = Registry.CurrentUser.OpenSubKey("Software\\ZhenhaiSoftware\\TCMInfomationExtraction", true);
            //if (RegRead == null)
            //{
            //    MessageBox.Show("请联系作者购买正版!");
            //    System.Environment.Exit(0);
            //}
            //else
            //{
            //    string KeyValue = RegRead.GetValue("SK").ToString();
            //    if (KeyValue == "")
            //    {
            //        MessageBox.Show("请联系作者购买正版!");
            //        System.Environment.Exit(0);
            //    }
            //    cert = RegRead.GetValue("SK").ToString();
            //    RegRead.Close();
            //}
            //myKey = EncryptStringToBytes_Aes(Encoding.Default.GetBytes(cert), rootkey, myIV); 


        }
        //////////////////////////////////////// </General>

        public MainWindow()
        {
            //////////////////////////////////////// <Validation>
            
            //////////////////////////////////////// <Initialize>
            InitializeComponent();
            ValidationCheck();
            XJTmainWindows.Title="兴灭继絶                请务必在 " + deadline + " 之前续费。过期将不可使用！";
            //////////////////////////////////////// <bingli>

            //////////////////////////////////////// <ZiLiaoJianSuo>
            ZiLiaoJianSuoBreakButton.Visibility = Visibility.Hidden;
            ZiLiaoJianSuoExtractionButton.IsEnabled = false;
            ZiLiaoJianSuoLibSearchBox.Text = "数据加载中，请稍候……";
            Task.Run(() => { ZiLiaoJianSuoLoadAllFilesAtBackground(); });
            //////////////////////////////////////// <ShuYuCiDian>
            ShuYuCiDianDict = (List<string[]>)ByteDeserialize(DecryptStringFromBytes_Aes(ReadFileToStream(SUITEWORKROOT + "xjt\\01.xjt"), MyAppKey, MYIV));
            BaiduFanyi.Visibility = Visibility.Hidden;
            //////////////////////////////////////// <JingLuoXueWei>
            Task.Run(() => { JingLuoXueWeiLoadLib(); });
            //////////////////////////////////////// <ZhongCaoYao>
            Task.Run(() => { ZhongCaoYaoLoadLib(); });
            //////////////////////////////////////// <FangJi>
            Task.Run(() => { FangJiLoadLib(); });
            //////////////////////////////////////// <ZhongYaoCha>
            Task.Run(() => { ZhongYaoChaLoadLib(); });
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //Thickness margin = new Thickness(10, 10, JingLuoXueWeiPicBigShow.Margin.Right, 10);
            ////JingLuoXueWeiPicBigShow.Margin = margin;
            JingLuoXueWeiPicBigShow.Height = JingLuoXueWeiXueWeiDetail.Height;
            ZhongCaoYaoPicBigShow.Height = ZhongCaoYaoCaoYaoDetail.Height;
        }


        //////////////////////////////////////// <BingLi>

        //////////////////////////////////////// </BingLi>


        //////////////////////////////////////// <ZiLiaoJianSuo>
        List<FileInfo> ZiLiaoJianSuoResultFileList = new List<FileInfo>();
        Dictionary<string, string[]> ZiLiaoJianSuoBookStruct = new Dictionary<string, string[]>();

        Dictionary<string, string> ZiLiaoJianSuoResultDict = new Dictionary<string, string>();
        bool ZiLiaoJianSuobreakout = false;

        private void ZiLiaoJianSuoLoadAllFilesAtBackground()
        {
            
            ZiLiaoJianSuoBookStruct = (Dictionary<string, string[]>)ByteDeserialize(DecryptStringFromBytes_Aes(ReadFileToStream(SUITEWORKROOT + "xjt\\02.xjt"), MyAppKey, MYIV));
            Dispatcher.Invoke(() => { ZiLiaoJianSuoLibSearchBox.Text = ""; ZiLiaoJianSuoExtractionButton.IsEnabled = true; });
        }

        private Task ZiLiaoJianSuoCaptureStringIntoResultDict(string[] KeyStrings, int RangeLength)
        {
            return Task.Run(() =>
            {
                try
                {
                    int i = 0;
                    //foreach (string Key in KeyStrings)
                    //{
                    //    KeysSumLen += Key.Length;
                    //}
                    if (KeyStrings.Count() == 1)
                    {
                        foreach (KeyValuePair<string, string[]> CurrentBook in ZiLiaoJianSuoBookStruct)
                        {
                            Dispatcher.Invoke(() => { ZiLiaoJianSuoResultLabel.Content = string.Format("进度：{0}", (((decimal)i + 1) / ZiLiaoJianSuoBookStruct.Count).ToString("P")); });
                            if (ZiLiaoJianSuobreakout == true) { break; }
                            string BookContent = "";
                            string CapturedString = "";
                            BookContent = CurrentBook.Value[2];
                            int ParaCount = 0;

                            if (CurrentBook.Key.IndexOf(KeyStrings[0]) != -1)
                            {
                                ZiLiaoJianSuoResultDict.Add(CurrentBook.Key, "单击右键载入全书。");
                                Dispatcher.Invoke(() =>
                                {
                                    FileListBox.Items.Add(CurrentBook.Key);
                                });
                            }

                            while (BookContent.Length > 0)
                            {
                                int KeyIndex = BookContent.IndexOf(KeyStrings[0]);
                                if (KeyIndex != -1)
                                {
                                    int start = KeyIndex;
                                    int end = start + KeyStrings[0].Length;
                                    if (start > RangeLength / 2) start = start - RangeLength / 2; else start = 0; ///////////////////////////////////////// 前后余量，平分 RangeLength 避免关键字卡边。
                                    if (BookContent.Length - end > RangeLength / 2) end = end + RangeLength / 2; else end = BookContent.Length;
                                    CapturedString += BookContent.Substring(start, end - start) + "\r\n" + "========================================" + "\r\n\r\n";
                                    ParaCount++;
                                    BookContent = BookContent.Substring(KeyIndex + KeyStrings[0].Length);
                                }
                                else break;
                            }

                            if (CapturedString != "")
                            {
                                ZiLiaoJianSuoResultDict.Add(ParaCount + ":" + CurrentBook.Key, CapturedString);
                                Dispatcher.Invoke(() =>
                                {
                                    FileListBox.Items.Add(ParaCount + ":" + CurrentBook.Key);
                                });
                            }
                            i++;
                        }
                    }
                    else
                    {
                        foreach (KeyValuePair<string, string[]> CurrentBook in ZiLiaoJianSuoBookStruct)
                        {
                            Dispatcher.Invoke(() => { ZiLiaoJianSuoResultLabel.Content = string.Format("进度：{0}", (((decimal)i + 1) / ZiLiaoJianSuoBookStruct.Count).ToString("P")); });
                            if (ZiLiaoJianSuobreakout == true) { break; }
                            string BookContent = "";
                            string CapturedString = "";
                            BookContent = CurrentBook.Value[2];
                            int[] KeysIndexes = new int[KeyStrings.Count()];
                            int ParaCount = 0;

                            while (BookContent.Length > 0)
                            {
                                for (int SubIndex = 0; SubIndex < KeysIndexes.Count(); SubIndex++)
                                {
                                    KeysIndexes[SubIndex] = BookContent.IndexOf(KeyStrings[SubIndex]);
                                }
                                Array.Sort(KeysIndexes);
                                if (KeysIndexes[0] != -1)
                                {
                                    if (KeysIndexes.Max() - KeysIndexes[0] > RangeLength)
                                    {
                                        BookContent = BookContent.Substring(KeysIndexes[0] + 1);
                                    }
                                    else
                                    {
                                        int start = KeysIndexes[0];
                                        int end = KeysIndexes.Max();
                                        if (start == end)
                                        {
                                            MessageBox.Show("关键字有重叠！请修改后重新抓取！");
                                            ZiLiaoJianSuobreakout = true;
                                            break;
                                        }
                                        if (start > RangeLength / 4) start = start - RangeLength / 4; else start = 0; ///////////////////////////////////////// 100 为前后余量，避免关键字卡边。
                                        if (BookContent.Length - end > RangeLength / 4) end = end + RangeLength / 4; else end = BookContent.Length;
                                        CapturedString += BookContent.Substring(start, end - start) + "\r\n" + "========================================" + "\r\n\r\n";
                                        ParaCount++;
                                        BookContent = BookContent.Substring(KeysIndexes.Max());
                                        //if (KeysIndexes.Max() > RangeLength / 4) BookContent = BookContent.Substring(KeysIndexes.Max() - RangeLength / 4); 
                                        //else BookContent = BookContent.Substring(KeysIndexes.Max()); //此处用KeysIndexes.Max()而不用end是因为end已经被加余量.
                                    }
                                }
                                else break;
                            }

                            if (CapturedString != "")
                            {
                                ZiLiaoJianSuoResultDict.Add(ParaCount + ":" + CurrentBook.Key, CapturedString);
                                Dispatcher.Invoke(() =>
                                {
                                    FileListBox.Items.Add(ParaCount + ":" + CurrentBook.Key);
                                });
                            }
                            i++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            });



            //    string TargetString = "";
            //    if (BookContent.Length > RangeLength) 
            //    { 
            //        TargetString = BookContent.Substring(0, RangeLength);
            //    }
            //    else
            //    {
            //        TargetString = BookContent;
            //    }
            //    bool Target = false;
            //    foreach (string s in KeyStrings)
            //    {
            //        if (TargetString.IndexOf(s) != -1)
            //        {
            //            Target = true;
            //        }
            //        else 
            //        {
            //            Target = false;
            //            break;
            //        }
            //    }
            //    if (Target)
            //    {
            //        CapturedString += TargetString + "\r\n" + "====================" + "\r\n\r\n";
            //        BookContent = BookContent.Substring(TargetString.Length / 2);
            //        ParaCount++;
            //    }
            //    else
            //    {
            //        BookContent = BookContent.Substring(1);
            //    }
            //}



            //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            //while (BookContent.Length > 0)
            //{
            //    int[] IndexesArry = new int[KeyStrings.Count()];
            //    for (int KeyIndex = 0; KeyIndex < KeyStrings.Count(); KeyIndex++)
            //    {
            //        if (BookContent.IndexOf(KeyStrings[KeyIndex]) != -1)
            //        {
            //            IndexesArry[KeyIndex] = BookContent.IndexOf(KeyStrings[KeyIndex]);
            //        }
            //        else IndexesArry[KeyIndex] = -1;
            //    }
            //    Array.Sort(IndexesArry);
            //    if (IndexesArry.Min() != -1)
            //    {
            //        if (IndexesArry.Max() - IndexesArry.Min() < RangeLength)
            //        {
            //            int start = 0;
            //            int end = BookContent.Length;
            //            if (IndexesArry.Min() > 100) start = IndexesArry.Min() - 100;
            //            if (BookContent.Length - IndexesArry.Max() > 100) end = IndexesArry.Max() + 100;
            //            CapturedString += BookContent.Substring(start, end - start) + "\r\n" + "====================" + "\r\n\r\n";
            //            BookContent = BookContent.Substring(IndexesArry[IndexesArry.Count() - 2]);
            //            ParaCount++;
            //        }
            //        else BookContent = BookContent.Substring(IndexesArry[1]);
            //    }
            //    else break;
            //}
            ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        }

        async private void ZiLiaoJianSuoExtractionButton_Click(object sender, RoutedEventArgs e)
        {
            FileListBox.Items.Clear();
            ZiLiaoJianSuoResultDict.Clear();
            ZiLiaoJianSuoExtractionButton.IsEnabled = false;
            if (ZiLiaoJianSuoLibSearchBox.Text != "")
            {
                ZiLiaoJianSuoBreakButton.Visibility = Visibility.Visible;
                int.TryParse(RangeBox.Text, out int RangeNo); //text转换成int数字
                string[] KeyStrings = ZiLiaoJianSuoLibSearchBox.Text.Split(' ').Where(s => !string.IsNullOrEmpty(s)).ToArray();//排空
                if (KeyStrings.Count() > 0 && RangeNo > 0)
                {
                    await ZiLiaoJianSuoCaptureStringIntoResultDict(KeyStrings, RangeNo);
                    ZiLiaoJianSuoResultLabel.Content = FileListBox.Items.Count + " 个结果！";
                }
                else MessageBox.Show("关键字 或 范围 无效！");
            }
            else
            {
                MessageBox.Show("请输入关键字！");
            }
            ZiLiaoJianSuoExtractionButton.IsEnabled = true;
            ZiLiaoJianSuoBreakButton.Visibility = Visibility.Hidden;
            ZiLiaoJianSuobreakout = false;
        }

        private void ZiLiaoJianSuoFileListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            FileListBox.IsEnabled = false;
            if (FileListBox.SelectedItem != null)
            {
                ResultTextBox.Document.Blocks.Clear();
                string[] KeyStrings = ZiLiaoJianSuoLibSearchBox.Text.Split(' ').Where(s => !string.IsNullOrEmpty(s)).ToArray();//排空
                if (KeyStrings.Count() > 0)
                {
                    ResultTextBox.Document.Blocks.Add(HighlightKeyStrings(ZiLiaoJianSuoResultDict[FileListBox.SelectedItem.ToString()], KeyStrings));
                    ResultTextBox.ScrollToHome();
                }
                else MessageBox.Show("关键字无效！");
            }
            else
            {
                MessageBox.Show("未选择文件或文件无效！");
            }
            FileListBox.IsEnabled = true;
        }

        private void ZiLiaoJianSuoBreakButton_Click(object sender, RoutedEventArgs e)
        {
            ZiLiaoJianSuobreakout = true;
            ZiLiaoJianSuoBreakButton.Visibility = Visibility.Hidden;
            MessageBox.Show("数据量大，后台完成任务后才可进行下一次抓取。");
        }

        private void FileListBox_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            FileListBox.IsEnabled = false;
            if (FileListBox.SelectedItem != null)
            {
                ResultTextBox.Document.Blocks.Clear();
                string[] KeyStrings = ZiLiaoJianSuoLibSearchBox.Text.Split(' ').Where(s => !string.IsNullOrEmpty(s)).ToArray();//排空
                if (KeyStrings.Count() > 0)
                {
                    string SelectedBookName = FileListBox.SelectedItem.ToString();
                    SelectedBookName = SelectedBookName.Substring(SelectedBookName.LastIndexOf(":") + 1);
                    ResultTextBox.Document.Blocks.Add(HighlightKeyStrings(ZiLiaoJianSuoBookStruct[SelectedBookName][2], KeyStrings));
                    ResultTextBox.ScrollToHome();
                }
                else MessageBox.Show("关键字无效！");
            }
            else
            {
                MessageBox.Show("未选择文件或文件无效！");
            }
            FileListBox.IsEnabled = true;
        }
        //////////////////////////////////////// </ZiLiaoJian>


        //////////////////////////////////////// <ShuYuCiDian>
        List<string[]> ShuYuCiDianDict = new List<string[]>();

        private void ShuYuCiDianSearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            string ShuYuCiDianLimitedInput = LimitedInput;
            int ShuYuCiDianIfLimit = -1;
            if (e.Key == Key.Enter)
            {
                BaiduFanyi.Visibility = Visibility.Hidden;
                string ShuYuCiDianSearchKey = ShuYuCiDianSearchBox.Text.ToLower();
                ResultList.Document.Blocks.Clear();
                ShuYuCiDianIfLimit = ShuYuCiDianLimitedInput.IndexOf(ShuYuCiDianSearchKey);

                if (ShuYuCiDianIfLimit != -1)
                {
                    Paragraph warninginfoP = new Paragraph();
                    Run warninginfoR = new Run();
                    warninginfoR.Text = "无效查询字符！输入至少2个字母或1个汉字！";
                    warninginfoR.FontSize = 16;
                    warninginfoP.Inlines.Add(warninginfoR);
                    ResultList.Document.Blocks.Add(warninginfoP);
                }
                else
                {
                    int TotalNumber = 0;
                    Paragraph ResultP = new Paragraph();
                    Run ResultR = new Run();
         

                    foreach (string[] item in ShuYuCiDianDict)
                    {
                        Paragraph ResultParapragh = new Paragraph();
                        Run FoundItem = new Run();
                        Run FoundDescription = new Run();
                        if (item[0].ToLower().IndexOf(ShuYuCiDianSearchKey) != -1)
                        {
                            FoundItem.Text = item[0];
                            FoundItem.Background = new SolidColorBrush(Color.FromRgb(128, 255, 128));
                            FoundItem.FontSize = 16;
                            FoundDescription.Text = "    ==> " + item[1];
                       
                            ResultParapragh.Inlines.Add(FoundItem);
                            ResultParapragh.Inlines.Add(FoundDescription);
                            ResultList.Document.Blocks.Add(ResultParapragh);
                            TotalNumber++;
                        }
                    }

                    ResultR.Text = "共找到" + TotalNumber.ToString() + "个结果！";
                    ResultR.Background = new SolidColorBrush(Color.FromRgb(128, 128, 255));
                    ResultR.FontSize = 16;
                    ResultP.Inlines.Add(ResultR);
                    if (ResultList.Document.Blocks.Count > 0)
                    {
                        ResultList.Document.Blocks.InsertBefore(ResultList.Document.Blocks.FirstBlock, ResultP);
                    }
                    else
                    {
                        ResultList.Document.Blocks.Add(ResultP);
                    }

                    ResultList.ScrollToHome();
                    ShuYuCiDianSearchBox.Focus();
                    //Licence!

                    if (TotalNumber == 0)
                    {
                        BaiduFanyi.Navigate("http://fanyi.baidu.com/?aldtype=85#zh/en/" + ShuYuCiDianSearchKey);
                        BaiduFanyi.Visibility = Visibility.Visible;
                    }

                }
            }
        }

        private void ShuYuCiDianAddNewButton_Click(object sender, RoutedEventArgs e)
        {
            if (ShuYuCiDianNewItem.Text !="" & ShuYuCiDianNewDescription.Text != "")
            {
                string[] NewItem = { ShuYuCiDianNewItem.Text, ShuYuCiDianNewDescription.Text };
                ShuYuCiDianDict.Add(NewItem);
                File.Delete(SUITEWORKROOT + "xjt\\01.xjt");
                WriteStreamToFile(SUITEWORKROOT + "xjt\\01.xjt", EncryptStringToBytes_Aes(ObjectSerialze(ShuYuCiDianDict),MyAppKey, MYIV));
                MessageBox.Show("已添加：\r\n" + ShuYuCiDianNewItem.Text);
                ShuYuCiDianNewItem.Text = "";
                ShuYuCiDianNewDescription.Text = "";
            }
            else
            {
                MessageBox.Show("条目或描述不全！");
            }
        }

        private void ShuYuCiDianCleanButton_Click(object sender, RoutedEventArgs e)
        {
            //ShuYuCiDianDict.RemoveAt(ShuYuCiDianDict.Count()-1);
            ShuYuCiDianNewItem.Text = "";
            ShuYuCiDianNewDescription.Text = "";
        }
        /// //////////////////////////////////////////////</ShuYuCiDian>


        //////////////////////////////////////// <JingLuoXueWei>
        Dictionary<string, string[]> JingLuoXueWeiTCMMeridianPoints = new Dictionary<string, string[]>();
        Dictionary<string, string[]> JingLuoXueWeiPointsPictures = new Dictionary<string, string[]>();
        Dictionary<string, string[]> JingLuoXueWeiTCMPointStruct = new Dictionary<string, string[]>();

        private void JingLuoXueWeiLoadLib()
        {
            
            JingLuoXueWeiTCMMeridianPoints = (Dictionary<string, string[]>)ByteDeserialize(DecryptStringFromBytes_Aes(ReadFileToStream(SUITEWORKROOT + "xjt\\03.xjt"), MyAppKey, MYIV));
            JingLuoXueWeiPointsPictures = (Dictionary<string, string[]>)ByteDeserialize(DecryptStringFromBytes_Aes(ReadFileToStream(SUITEWORKROOT + "xjt\\04.xjt"), MyAppKey, MYIV));
            JingLuoXueWeiTCMPointStruct = (Dictionary<string, string[]>)ByteDeserialize(DecryptStringFromBytes_Aes(ReadFileToStream(SUITEWORKROOT + "xjt\\05.xjt"), MyAppKey, MYIV));
            foreach (var i in JingLuoXueWeiTCMMeridianPoints)
            {
                Dispatcher.Invoke(() => { JingLuoXueWeiJingLuoList.Items.Add(i.Key); });
            }

        }

        async private void JingLuoXueWeiXueWeiSearch_Click(object sender, RoutedEventArgs e)
        {
            JingLuoXueWeiXueWeiSearch.IsEnabled = false;
            JingLuoXueWeiXueWeiList.Items.Clear();
            if (JingLuoXueWeiPointSearch.Text != "")
            {

                string[] KeyStrings = JingLuoXueWeiPointSearch.Text.Split(' ').Where(s => !string.IsNullOrEmpty(s)).ToArray();//排空
                if (KeyStrings.Count() > 0)
                {
                    await JingLuoXueWeiPointGoSearch(KeyStrings);
                }
                else MessageBox.Show("关键字 无效！");
            }
            else
            {
                MessageBox.Show("请输入关键字！");
            }
            JingLuoXueWeiXueWeiSearch.IsEnabled = true;
        }

        private Task JingLuoXueWeiPointGoSearch(string[] KeyStrings)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (KeyStrings.Count() == 1)
                    {
                        foreach (KeyValuePair<string, string[]> CurrentPoint in JingLuoXueWeiTCMPointStruct)
                        {
                            if (CurrentPoint.Value[2].IndexOf(KeyStrings[0]) != -1)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    JingLuoXueWeiXueWeiList.Items.Add(CurrentPoint.Key);
                                });
                            }
                        }
                    }
                    else
                    {
                        foreach (KeyValuePair<string, string[]> CurrentPoint in JingLuoXueWeiTCMPointStruct)
                        {
                            bool IfTarget = false;
                            foreach (string keyword in KeyStrings)
                            {
                                if (CurrentPoint.Value[2].IndexOf(keyword) != -1)
                                {
                                    IfTarget = true;
                                }
                                else
                                {
                                    IfTarget = false;
                                    break;
                                }
                            }
                            if (IfTarget)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    JingLuoXueWeiXueWeiList.Items.Add(CurrentPoint.Key);
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            });
        }

        private void JingLuoXueWeiPic1_MediaEnded(object sender, RoutedEventArgs e)
        {
            JingLuoXueWeiPic1.Position = TimeSpan.FromMilliseconds(1);
            JingLuoXueWeiPic1.Play();
        }

        private void JingLuoXueWeiPic1_MouseEnter(object sender, MouseEventArgs e)
        {
            JingLuoXueWeiPicBigShow.Source = JingLuoXueWeiPic1.Source;
        }

        private void JingLuoXueWeiPic1_MouseLeave(object sender, MouseEventArgs e)
        {
            JingLuoXueWeiPicBigShow.Source = null;
        }

        private void JingLuoXueWeiPic2_MediaEnded(object sender, RoutedEventArgs e)
        {
            JingLuoXueWeiPic2.Position = TimeSpan.FromMilliseconds(1);
            JingLuoXueWeiPic2.Play();
        }

        private void JingLuoXueWeiPic2_MouseEnter(object sender, MouseEventArgs e)
        {
            JingLuoXueWeiPicBigShow.Source = JingLuoXueWeiPic2.Source;
        }

        private void JingLuoXueWeiPic2_MouseLeave(object sender, MouseEventArgs e)
        {
            JingLuoXueWeiPicBigShow.Source = null;
        }

        private void JingLuoXueWeiPic3_MediaEnded(object sender, RoutedEventArgs e)
        {
            JingLuoXueWeiPic3.Position = TimeSpan.FromMilliseconds(1);
            JingLuoXueWeiPic3.Play();
        }

        private void JingLuoXueWeiPic3_MouseEnter(object sender, MouseEventArgs e)
        {
            JingLuoXueWeiPicBigShow.Source = JingLuoXueWeiPic3.Source;
        }

        private void JingLuoXueWeiPic3_MouseLeave(object sender, MouseEventArgs e)
        {
            JingLuoXueWeiPicBigShow.Source = null;
        }

        private void JingLuoXueWeiPic4_MediaEnded(object sender, RoutedEventArgs e)
        {
            JingLuoXueWeiPic4.Position = TimeSpan.FromMilliseconds(1);
            JingLuoXueWeiPic4.Play();
        }

        private void JingLuoXueWeiPic4_MouseEnter(object sender, MouseEventArgs e)
        {
            JingLuoXueWeiPicBigShow.Source = JingLuoXueWeiPic4.Source;
        }

        private void JingLuoXueWeiPic4_MouseLeave(object sender, MouseEventArgs e)
        {
            JingLuoXueWeiPicBigShow.Source = null;
        }

        private void JingLuoXueWeiPicBigShow_MediaEnded(object sender, RoutedEventArgs e)
        {
            JingLuoXueWeiPicBigShow.Position = TimeSpan.FromMilliseconds(1);
            JingLuoXueWeiPicBigShow.Play();
        }

        private void JingLuoXueWeiPicBigShow_MouseLeave(object sender, MouseEventArgs e)
        {
            JingLuoXueWeiPicBigShow.Source = null;
        }

        private void JingLuoXueWeiJingLuoList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (JingLuoXueWeiJingLuoList.SelectedItem != null)
            {
                JingLuoXueWeiXueWeiList.Items.Clear();
                string[] JingLuoXueWeiXueWeiInCurrentJingLuo = JingLuoXueWeiTCMMeridianPoints[JingLuoXueWeiJingLuoList.SelectedItem.ToString()];
                foreach (string XueWei in JingLuoXueWeiXueWeiInCurrentJingLuo)
                {
                    JingLuoXueWeiXueWeiList.Items.Add(XueWei);
                }
            }
            else
            {
                MessageBox.Show("未选中经脉！");
            }
        }

        private void JingLuoXueWeiXueWeiList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (JingLuoXueWeiXueWeiList.SelectedItem != null)
            {
                JingLuoXueWeiXueWeiDetail.Document.Blocks.Clear();
                JingLuoXueWeiPic1.Source = null;
                JingLuoXueWeiPic2.Source = null;
                JingLuoXueWeiPic3.Source = null;
                JingLuoXueWeiPic4.Source = null;
                string XueWeiName = JingLuoXueWeiXueWeiList.SelectedItem.ToString();
                JingLuoXueWeiJingLuoList.SelectedItem = JingLuoXueWeiTCMPointStruct[XueWeiName][1];
                if (JingLuoXueWeiPointSearch.Text != "")
                {
                    string[] keywords = JingLuoXueWeiPointSearch.Text.Split(' ').ToArray().Where(s => !string.IsNullOrEmpty(s)).ToArray().Union(JingLuoXueWeiTCMPointStruct.Keys.ToArray()).ToArray();
                    JingLuoXueWeiXueWeiDetail.Document.Blocks.Add(HighlightKeyStrings(JingLuoXueWeiTCMPointStruct[XueWeiName][2], keywords));
                }
                else
                {
                    string[] keywords = JingLuoXueWeiTCMPointStruct.Keys.ToArray();
                    JingLuoXueWeiXueWeiDetail.Document.Blocks.Add(HighlightKeyStrings(JingLuoXueWeiTCMPointStruct[XueWeiName][2], keywords));
                }
                JingLuoXueWeiXueWeiDetail.ScrollToHome();
                if (JingLuoXueWeiPointsPictures.Keys.Contains(XueWeiName))
                {
                    string[] pictures = JingLuoXueWeiPointsPictures[XueWeiName];

                    switch (pictures.Count())
                    {
                        case 1:
                            JingLuoXueWeiPic1.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[0], UriKind.Absolute);
                            break;
                        case 2:
                            JingLuoXueWeiPic1.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[0], UriKind.Absolute);
                            JingLuoXueWeiPic2.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[1], UriKind.Absolute);
                            break;
                        case 3:
                            JingLuoXueWeiPic1.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[0], UriKind.Absolute);
                            JingLuoXueWeiPic2.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[1], UriKind.Absolute);
                            JingLuoXueWeiPic3.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[2], UriKind.Absolute);
                            break;
                        case 4:
                            JingLuoXueWeiPic1.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[0], UriKind.Absolute);
                            JingLuoXueWeiPic2.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[1], UriKind.Absolute);
                            JingLuoXueWeiPic3.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[2], UriKind.Absolute);
                            JingLuoXueWeiPic4.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[3], UriKind.Absolute);
                            break;
                    }
                }
            }
            else
            {
                MessageBox.Show("未选中穴位！");
            }
        }
        //////////////////////////////////////////////////////////////////////////////////// </JingLuoXueWei>


        //////////////////////////////////////////////////////////////////////////////////// <ZhongCaoYao>
        Dictionary<string, string[]> ZhongCaoYaoTCMHerbStruct = new Dictionary<string, string[]>();
        Dictionary<string, string[]> ZhongCaoYaoHerbsPictures = new Dictionary<string, string[]>();

        private Task ZhongCaoYaoGoSearch(string[] KeyStrings)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (KeyStrings.Count() == 1)
                    {
                        foreach (KeyValuePair<string, string[]> CurrentHerb in ZhongCaoYaoTCMHerbStruct)
                        {
                            if (CurrentHerb.Value[1].IndexOf(KeyStrings[0]) != -1)
                            {
                                string FoundHerb = CurrentHerb.Key;
                                if (ZhongCaoYaoHerbsPictures.ContainsKey(FoundHerb))
                                {
                                    FoundHerb = "# " + FoundHerb;
                                }
                                Dispatcher.Invoke(() =>
                                {
                                    ZhongCaoYaoSearchCaoYaoList.Items.Add(FoundHerb);
                                });
                            }
                        }
                    }
                    else
                    {
                        foreach (KeyValuePair<string, string[]> CurrentHerb in ZhongCaoYaoTCMHerbStruct)
                        {
                            //string[] a=CurrentHerb.Value;
                            bool IfTarget = false;
                            string range = CurrentHerb.Value[0] + CurrentHerb.Value[2] + CurrentHerb.Value[3] + CurrentHerb.Value[4];
                            foreach (string keyword in KeyStrings)
                            {
                                if (range.IndexOf(keyword) != -1)
                                {
                                    IfTarget = true;
                                }
                                else
                                {
                                    IfTarget = false;
                                    break;
                                }
                            }
                            if (IfTarget)
                            {
                                string FoundHerb = CurrentHerb.Key;
                                if (ZhongCaoYaoHerbsPictures.ContainsKey(FoundHerb))
                                {
                                    FoundHerb = "# " + FoundHerb;
                                }
                                Dispatcher.Invoke(() =>
                                {
                                    ZhongCaoYaoSearchCaoYaoList.Items.Add(FoundHerb);
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            });
        }

        private void ZhongCaoYaoLoadLib()
        {
            ZhongCaoYaoTCMHerbStruct = (Dictionary<string, string[]>)ByteDeserialize(DecryptStringFromBytes_Aes(ReadFileToStream(SUITEWORKROOT + "xjt\\06.xjt"), MyAppKey, MYIV));
            ZhongCaoYaoHerbsPictures = (Dictionary<string, string[]>)ByteDeserialize(DecryptStringFromBytes_Aes(ReadFileToStream(SUITEWORKROOT + "xjt\\07.xjt"), MyAppKey, MYIV));
            string[] Herbs = ZhongCaoYaoTCMHerbStruct.Keys.ToArray();
            Array.Sort(Herbs);
            foreach (var i in Herbs)
            {
                string zhongcaoyao = i;
                if (ZhongCaoYaoHerbsPictures.ContainsKey(zhongcaoyao))
                {
                    zhongcaoyao = "# " + zhongcaoyao;
                }
                Dispatcher.Invoke(() => { ZhongCaoYaoCaoYaoList.Items.Add(zhongcaoyao); });

            }
            //foreach (var i in ZhongCaoYaoTCMHerbStruct)
            //{
            //    string zhuzhi = i.Value[1];
            //    string tap = "【性味】";
            //    int start = zhuzhi.IndexOf(tap) + 4;
            //    zhuzhi = zhuzhi.Substring(start, zhuzhi.IndexOf("\r\n", start) - start);
            //    i.Value[2] = zhuzhi;

            //    string dingwei = i.Value[1];
            //    string tbp = "【功能主治】";
            //    int start2 = dingwei.IndexOf(tbp) + 6;
            //    dingwei = dingwei.Substring(start2, dingwei.IndexOf("\r\n", start2) - start2);
            //    i.Value[3] = dingwei;

            //    string caozuo = i.Value[1];
            //    string tcp = "【别名】";
            //    int start3 = caozuo.IndexOf(tcp) + 4;
            //    caozuo = caozuo.Substring(start3, caozuo.IndexOf("\r\n", start3) - start3);
            //    i.Value[4] = caozuo;
            //}
            //MessageBox.Show("finished");
        }

        private void ZhongCaoYaoPic1_MediaEnded(object sender, RoutedEventArgs e)
        {
            ZhongCaoYaoPic1.Position = TimeSpan.FromMilliseconds(1);
            ZhongCaoYaoPic1.Play();
        }

        private void ZhongCaoYaoPic1_MouseEnter(object sender, MouseEventArgs e)
        {
            ZhongCaoYaoPicBigShow.Source = ZhongCaoYaoPic1.Source;
        }

        private void ZhongCaoYaoPic1_MouseLeave(object sender, MouseEventArgs e)
        {
            ZhongCaoYaoPicBigShow.Source = null;
        }

        private void ZhongCaoYaoPic2_MediaEnded(object sender, RoutedEventArgs e)
        {
            ZhongCaoYaoPic2.Position = TimeSpan.FromMilliseconds(1);
            ZhongCaoYaoPic2.Play();
        }

        private void ZhongCaoYaoPic2_MouseEnter(object sender, MouseEventArgs e)
        {
            ZhongCaoYaoPicBigShow.Source = ZhongCaoYaoPic2.Source;
        }

        private void ZhongCaoYaoPic2_MouseLeave(object sender, MouseEventArgs e)
        {
            ZhongCaoYaoPicBigShow.Source = null;
        }

        private void ZhongCaoYaoPic3_MediaEnded(object sender, RoutedEventArgs e)
        {
            ZhongCaoYaoPic3.Position = TimeSpan.FromMilliseconds(1);
            ZhongCaoYaoPic3.Play();
        }

        private void ZhongCaoYaoPic3_MouseEnter(object sender, MouseEventArgs e)
        {
            ZhongCaoYaoPicBigShow.Source = ZhongCaoYaoPic3.Source;
        }

        private void ZhongCaoYaoPic3_MouseLeave(object sender, MouseEventArgs e)
        {
            ZhongCaoYaoPicBigShow.Source = null;
        }

        private void ZhongCaoYaoPic4_MediaEnded(object sender, RoutedEventArgs e)
        {
            ZhongCaoYaoPic4.Position = TimeSpan.FromMilliseconds(1);
            ZhongCaoYaoPic4.Play();
        }

        private void ZhongCaoYaoPic4_MouseEnter(object sender, MouseEventArgs e)
        {
            ZhongCaoYaoPicBigShow.Source = ZhongCaoYaoPic4.Source;
        }

        private void ZhongCaoYaoPic4_MouseLeave(object sender, MouseEventArgs e)
        {
            ZhongCaoYaoPicBigShow.Source = null;
        }

        private void ZhongCaoYaoPicBigShow_MediaEnded(object sender, RoutedEventArgs e)
        {
            ZhongCaoYaoPicBigShow.Position = TimeSpan.FromMilliseconds(1);
            ZhongCaoYaoPicBigShow.Play();
        }

        private void ZhongCaoYaoPicBigShow_MouseLeave(object sender, MouseEventArgs e)
        {
            ZhongCaoYaoPicBigShow.Source = null;
        }

        private void ZhongCaoYaoCaoYaoList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ZhongCaoYaoCaoYaoList.SelectedItem != null)
            {
                ZhongCaoYaoCaoYaoDetail.Document.Blocks.Clear();
                ZhongCaoYaoPic1.Source = null;
                ZhongCaoYaoPic2.Source = null;
                ZhongCaoYaoPic3.Source = null;
                ZhongCaoYaoPic4.Source = null;
                string HerbName = ZhongCaoYaoCaoYaoList.SelectedItem.ToString().Replace("# ",""); ;
                string[] keyname = { HerbName };
                ZhongCaoYaoCaoYaoDetail.Document.Blocks.Add(HighlightKeyStrings(ZhongCaoYaoTCMHerbStruct[HerbName][1],keyname));
                ZhongCaoYaoCaoYaoDetail.ScrollToHome();
                if (ZhongCaoYaoHerbsPictures.Keys.Contains(HerbName))
                {
                    string[] pictures = ZhongCaoYaoHerbsPictures[HerbName];

                    switch (pictures.Count())
                    {
                        case 1:
                            ZhongCaoYaoPic1.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[0], UriKind.Absolute);
                            break;
                        case 2:
                            ZhongCaoYaoPic1.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[0], UriKind.Absolute);
                            ZhongCaoYaoPic2.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[1], UriKind.Absolute);
                            break;
                        case 3:
                            ZhongCaoYaoPic1.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[0], UriKind.Absolute);
                            ZhongCaoYaoPic2.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[1], UriKind.Absolute);
                            ZhongCaoYaoPic3.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[2], UriKind.Absolute);
                            break;
                        case 4:
                            ZhongCaoYaoPic1.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[0], UriKind.Absolute);
                            ZhongCaoYaoPic2.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[1], UriKind.Absolute);
                            ZhongCaoYaoPic3.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[2], UriKind.Absolute);
                            ZhongCaoYaoPic4.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[3], UriKind.Absolute);
                            break;
                    }
                }




            }
            else
            {
                MessageBox.Show("未选中中草药！");
            }
        }

        private void ZhongCaoYaoSearchCaoYaoList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ZhongCaoYaoSearchCaoYaoList.SelectedItem != null)
            {
                ZhongCaoYaoCaoYaoDetail.Document.Blocks.Clear();
                ZhongCaoYaoPic1.Source = null;
                ZhongCaoYaoPic2.Source = null;
                ZhongCaoYaoPic3.Source = null;
                ZhongCaoYaoPic4.Source = null;
                string HerbName = ZhongCaoYaoSearchCaoYaoList.SelectedItem.ToString().Replace("# ", "");
                string[] KeyStrings = ZhongCaoYaoSearchBox.Text.Split(' ').Where(s => !string.IsNullOrEmpty(s)).ToArray();
                ZhongCaoYaoCaoYaoDetail.Document.Blocks.Add(HighlightKeyStrings(ZhongCaoYaoTCMHerbStruct[HerbName][1], KeyStrings));
                ZhongCaoYaoCaoYaoDetail.ScrollToHome();
                if (ZhongCaoYaoHerbsPictures.Keys.Contains(HerbName))
                {
                    string[] pictures = ZhongCaoYaoHerbsPictures[HerbName];

                    switch (pictures.Count())
                    {
                        case 1:
                            ZhongCaoYaoPic1.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[0], UriKind.Absolute);
                            break;
                        case 2:
                            ZhongCaoYaoPic1.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[0], UriKind.Absolute);
                            ZhongCaoYaoPic2.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[1], UriKind.Absolute);
                            break;
                        case 3:
                            ZhongCaoYaoPic1.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[0], UriKind.Absolute);
                            ZhongCaoYaoPic2.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[1], UriKind.Absolute);
                            ZhongCaoYaoPic3.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[2], UriKind.Absolute);
                            break;
                        case 4:
                            ZhongCaoYaoPic1.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[0], UriKind.Absolute);
                            ZhongCaoYaoPic2.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[1], UriKind.Absolute);
                            ZhongCaoYaoPic3.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[2], UriKind.Absolute);
                            ZhongCaoYaoPic4.Source = new Uri(SUITEWORKROOT + "pic\\" + pictures[3], UriKind.Absolute);
                            break;
                    }
                }
            }
            else
            {
                MessageBox.Show("未选中中草药！");
            }
        }

        async private void ZhongCaoYaoSearchButton_Click(object sender, RoutedEventArgs e)
        {
            ZhongCaoYaoSearchButton.IsEnabled = false;
            ZhongCaoYaoSearchCaoYaoList.Items.Clear();
            if (ZhongCaoYaoSearchBox.Text != "")
            {

                string[] KeyStrings = ZhongCaoYaoSearchBox.Text.Split(' ').Where(s => !string.IsNullOrEmpty(s)).ToArray();//排空
                if (KeyStrings.Count() > 0)
                {
                    await ZhongCaoYaoGoSearch(KeyStrings);
                }
                else MessageBox.Show("关键字 无效！");
            }
            else
            {
                MessageBox.Show("请输入关键字！");
            }
            ZhongCaoYaoSearchButton.IsEnabled = true;

        }
        //////////////////////////////////////////////////////////////////////////////////// </ZhongCaoYao>


        //////////////////////////////////////////////////////////////////////////////////// <FangJi>
        Dictionary<string, string[]> FangJiTCMPrescriptionStruct = new Dictionary<string, string[]>();
        Dictionary<string, string[]> FangJiTCMPrescriptionTypeClass = new Dictionary<string, string[]>();
        Dictionary<string, string[]> FangJiTCMPrescriptionClassPrescription = new Dictionary<string, string[]>();

        private void FangJiLoadLib()
        {
            FangJiTCMPrescriptionStruct = (Dictionary<string, string[]>)ByteDeserialize(DecryptStringFromBytes_Aes(ReadFileToStream(SUITEWORKROOT + "xjt\\08.xjt"), MyAppKey, MYIV));
            FangJiTCMPrescriptionTypeClass = (Dictionary<string, string[]>)ByteDeserialize(DecryptStringFromBytes_Aes(ReadFileToStream(SUITEWORKROOT + "xjt\\09.xjt"), MyAppKey, MYIV));
            FangJiTCMPrescriptionClassPrescription = (Dictionary<string, string[]>)ByteDeserialize(DecryptStringFromBytes_Aes(ReadFileToStream(SUITEWORKROOT + "xjt\\10.xjt"), MyAppKey, MYIV));
            foreach(var FJType in FangJiTCMPrescriptionTypeClass)
            {
                Dispatcher.Invoke(() => { FangJiPrescriptionTypeList.Items.Add(FJType.Key); });
            }
        }

        private void FangJiPrescriptionTypeList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FangJiPrescriptionTypeList.SelectedItem != null)
            {
                FangJiPrescriptionClassList.Items.Clear();
                string[] Pclasses = FangJiTCMPrescriptionTypeClass[FangJiPrescriptionTypeList.SelectedItem.ToString()];
                foreach (string Pclass in Pclasses)
                {
                    FangJiPrescriptionClassList.Items.Add(Pclass);
                }
            }
            else
            {
                MessageBox.Show("未选中方剂类别！");
            }
        }

        private void FangJiPrescriptionClassList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FangJiPrescriptionClassList.SelectedItem != null)
            {
                FangJiPrescriptionNameList.Items.Clear();
                string[] Pnames = FangJiTCMPrescriptionClassPrescription[FangJiPrescriptionClassList.SelectedItem.ToString()];
                foreach (string prescription in Pnames)
                {
                    FangJiPrescriptionNameList.Items.Add(prescription);
                }
            }
            else
            {
                MessageBox.Show("未选中方剂功效！");
            }
        }

        private void FangJiPrescriptionNameList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FangJiPrescriptionNameList.SelectedItem != null)
            {
                FangJiPrescriptionDetail.Document.Blocks.Clear();
                string Pname = FangJiPrescriptionNameList.SelectedItem.ToString();
                FangJiPrescriptionTypeList.SelectedItem = FangJiTCMPrescriptionStruct[Pname][5];
                FangJiPrescriptionClassList.SelectedItem = FangJiTCMPrescriptionStruct[Pname][6];
               
                if (FangJiPrescriptionSearchTextBox.Text != "")
                {
                    string[] keywords = FangJiPrescriptionSearchTextBox.Text.Split(' ').ToArray().Where(s => !string.IsNullOrEmpty(s)).ToArray().Union(FangJiTCMPrescriptionStruct.Keys.ToArray()).ToArray();
                    FangJiPrescriptionDetail.Document.Blocks.Add(HighlightKeyStrings(FangJiTCMPrescriptionStruct[Pname][1], keywords));
                }
                else
                {
                    FangJiPrescriptionDetail.Document.Blocks.Add(HighlightKeyStrings(FangJiTCMPrescriptionStruct[Pname][1], FangJiTCMPrescriptionStruct.Keys.ToArray()));
                }
                FangJiPrescriptionDetail.ScrollToHome();
            }
            else
            {
                MessageBox.Show("未选中方剂！");
            }
        }

        private void FangJiPrescriptionTypeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FangJiPrescriptionTypeList.SelectedItem != null)
            {
                FangJiPrescriptionClassList.Items.Clear();
                string[] Pclasses = FangJiTCMPrescriptionTypeClass[FangJiPrescriptionTypeList.SelectedItem.ToString()];
                foreach (string Pclass in Pclasses)
                {
                    FangJiPrescriptionClassList.Items.Add(Pclass);
                }
            }
        }

        async private void FangJiPrescriptionSearchButton_Click(object sender, RoutedEventArgs e)
        {
            FangJiPrescriptionSearchButton.IsEnabled = false;
            FangJiPrescriptionNameList.Items.Clear();
            if (FangJiPrescriptionSearchTextBox.Text != "")
            {
                string[] KeyStrings = FangJiPrescriptionSearchTextBox.Text.Split(' ').Where(s => !string.IsNullOrEmpty(s)).ToArray();//排空
                if (KeyStrings.Count() > 0)
                {
                    await FangJiGoSearch(KeyStrings);
                }
                else MessageBox.Show("关键字 无效！");
            }
            else
            {
                MessageBox.Show("请输入关键字！");
            }
            FangJiPrescriptionSearchButton.IsEnabled = true;
        }

        private Task FangJiGoSearch(string[] KeyStrings)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (KeyStrings.Count() == 1)
                    {
                        foreach (KeyValuePair<string, string[]> CurrentPrescription in FangJiTCMPrescriptionStruct)
                        {
                            string[] values = CurrentPrescription.Value;
                            string content = values[1];

                            if (content.IndexOf(KeyStrings[0]) != -1)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    FangJiPrescriptionNameList.Items.Add(CurrentPrescription.Key);
                                });
                            }
                        }
                    }
                    else
                    {
                        foreach (KeyValuePair<string, string[]> CurrentPrescription in FangJiTCMPrescriptionStruct)
                        {
                            bool IfTarget = false;
                            string[] values = CurrentPrescription.Value;
                            string content = "";

                            content = values[0] + values[5] + values[6] + values[7] + values[8] + values[10] + values[11];
                            foreach (string keyword in KeyStrings)
                            {
                                if (content.IndexOf(keyword) != -1)
                                {
                                    IfTarget = true;
                                }
                                else
                                {
                                    IfTarget = false;
                                    break;
                                }
                            }
                            if (IfTarget)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    FangJiPrescriptionNameList.Items.Add(CurrentPrescription.Key);
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            });
        }
        //////////////////////////////////////////////////////////////////////////////////// </FangJi>


        //////////////////////////////////////////////////////////////////////////////////// <ZhongYaoCha>
        Dictionary<string, string[]> TCMTeasStruct = new Dictionary<string, string[]>();
        Dictionary<string, string[]> TCMTeasTypes = new Dictionary<string, string[]>();

        private void ZhongYaoChaLoadLib()
        {
            TCMTeasStruct = (Dictionary<string, string[]>)ByteDeserialize(DecryptStringFromBytes_Aes(ReadFileToStream(SUITEWORKROOT + "xjt\\11.xjt"), MyAppKey, MYIV));
            TCMTeasTypes = (Dictionary<string, string[]>)ByteDeserialize(DecryptStringFromBytes_Aes(ReadFileToStream(SUITEWORKROOT + "xjt\\12.xjt"), MyAppKey, MYIV));

            foreach (var i in TCMTeasTypes)
            {
                Dispatcher.Invoke(()=> { ZhongYaoChaTypeList.Items.Add(i.Key); });
            }
        }

        private Task ZhongYaoChaGoSearch(string[] KeyStrings)
        {
            return Task.Run(() =>
            {
                try
                {
                    if (KeyStrings.Count() == 1)
                    {
                        foreach (KeyValuePair<string, string[]> CurrentTea in TCMTeasStruct)
                        {
                            string[] values = CurrentTea.Value;
                            string content = "";
                            foreach (string s in values)
                            {
                                content += s;
                            }

                            if (content.IndexOf(KeyStrings[0]) != -1)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    ZhongYaoChaNameList.Items.Add(CurrentTea.Key);
                                });
                            }
                        }
                    }
                    else
                    {
                        foreach (KeyValuePair<string, string[]> CurrentTea in TCMTeasStruct)
                        {
                            bool IfTarget = false;
                            string[] values = CurrentTea.Value;
                            string content = "";
                            foreach (string s in values)
                            {
                                content += s;
                            }
                            foreach (string keyword in KeyStrings)
                            {
                                if (content.IndexOf(keyword) != -1)
                                {
                                    IfTarget = true;
                                }
                                else
                                {
                                    IfTarget = false;
                                    break;
                                }
                            }
                            if (IfTarget)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    ZhongYaoChaNameList.Items.Add(CurrentTea.Key);
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            });
        }
    
        private void ZhongYaoChaTypeList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ZhongYaoChaTypeList.SelectedItem != null)
            {
                ZhongYaoChaNameList.Items.Clear();
                string[] Teas = TCMTeasTypes[ZhongYaoChaTypeList.SelectedItem.ToString()];
                foreach (string tea in Teas)
                {
                    ZhongYaoChaNameList.Items.Add(tea);
                }
            }
            else
            {
                MessageBox.Show("未选中中药茶类别！");
            }
        }

        private void ZhongYaoChaNameList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ZhongYaoChaNameList.SelectedItem != null)
            {
                ZhongYaoChaDetail.Document.Blocks.Clear();
                string Teaname = ZhongYaoChaNameList.SelectedItem.ToString();
                ZhongYaoChaTypeList.SelectedItem = TCMTeasStruct[Teaname][6];
                string[] values = TCMTeasStruct[Teaname];
                string content = "";
                foreach (string s in values)
                {
                    content += s+"\r\n";
                }
               
                if (ZhongYaoChaSearchTextBox.Text != "")
                {
                    string[] keywords = ZhongYaoChaSearchTextBox.Text.Split(' ').ToArray().Where(s => !string.IsNullOrEmpty(s)).ToArray().Union(FangJiTCMPrescriptionStruct.Keys.ToArray()).ToArray();
                    ZhongYaoChaDetail.Document.Blocks.Add(HighlightKeyStrings(content, keywords));
                }
                else
                {
                    ZhongYaoChaDetail.Document.Blocks.Add(HighlightKeyStrings(content, TCMTeasStruct.Keys.ToArray()));
                }
            }
            else
            {
                MessageBox.Show("未选中中药茶！");
            }
        }

        async private void ZhongYaoChaSearchButton_Click(object sender, RoutedEventArgs e)
        {
            ZhongYaoChaSearchButton.IsEnabled = false;
            ZhongYaoChaNameList.Items.Clear();
            if (ZhongYaoChaSearchTextBox.Text != "")
            {

                string[] KeyStrings = ZhongYaoChaSearchTextBox.Text.Split(' ').Where(s => !string.IsNullOrEmpty(s)).ToArray();//排空
                if (KeyStrings.Count() > 0)
                {
                    await ZhongYaoChaGoSearch(KeyStrings);
                }
                else MessageBox.Show("关键字 无效！");
            }
            else
            {
                MessageBox.Show("请输入关键字！");
            }
            ZhongYaoChaSearchButton.IsEnabled = true;
        }
        ////////////////////////////////////////////////////////////////////////////////////</ZhongYaoCha>


        ////////////////////////////////////////////////////////////////////////////////////<Other>

        ////////////////////////////////////////////////////////////////////////////////////</Other>


        ////////////////////////////////////////////////////////////////////////////////////<Other>

        ////////////////////////////////////////////////////////////////////////////////////</Other>

        
        private void Newstreamcreator_Click(object sender, RoutedEventArgs e)
        {
            //int n = 10000;
            //foreach( var xuewei in JingLuoXueWeiPointsPictures)
            //{
            //    for (int i = 0; i < xuewei.Value.Count(); i++)
            //    {
            //        string fullname = xuewei.Value[i];
            //        xuewei.Value[i] = fullname.Replace(fullname.Substring(0, fullname.IndexOf('.')), "P" + n.ToString());
            //        File.Move(SUITEWORKROOT + "pic\\" + fullname, SUITEWORKROOT + "pic\\" + xuewei.Value[i]);
            //        n++;
            //    }

            //}
            //File.Delete(SUITEWORKROOT + "xjt\\HerbsPictures.xjt");
            //WriteStreamToFile(SUITEWORKROOT + "xjt\\HerbsPictures.xjt", ObjectSerialze(ZhongCaoYaoHerbsPictures));



            //string[] indicationList =(string[]) ByteDeserialize(ReadFileToStream(SUITEWORKROOT + "xjt\\13.xjt"));
            //Dictionary<string, int> herbF = (Dictionary<string,int>)ByteDeserialize(ReadFileToStream(SUITEWORKROOT + "xjt\\14.xjt"));
            //string zhusu = showboardA.Text;

            //showboardB.Text = "";
            //foreach(char divide in LimitedInput)
            //{
            //    zhusu = zhusu.Replace(divide, '@');
            //}
            //string[] zhusuindication = zhusu.Split('@').Where(s => !string.IsNullOrEmpty(s)).ToArray();

            //List<string> foundindicatorList = new List<string>();
            //List<string> foundPrescription = new List<string>();

            //foreach (string indicator in indicationList)
            //{
            //    if (zhusu.IndexOf(indicator) != -1)
            //    {
            //        //int max1 = 2;
            //        //int max2 = 1;
            //        //string maxA = "";
            //        //string maxB = "";
            //        foundindicatorList.Add(indicator);
            //        //showboardB.Text += "\r\n" + indicator + ":=>\t";
            //        //zhusu = zhusu.Replace(indicator, "");
            //        //foreach (var prescription in FangJiTCMPrescriptionStruct)
            //        //{

            //        //    if (prescription.Value[11].IndexOf(indicator) != -1)
            //        //    {
            //        //        //if (!foundindHerbList.Contains(herb.Key))
            //        //        ////{
            //        //        //    foundindHerbList.Add(herb.Key);
            //        //        //    if (herbF[herb.Key] > max1)
            //        //        //    {
            //        //        //        max1 = herbF[herb.Key];
            //        //        //    maxA = herb.Key;// + " " + herbF[herb.Key].ToString();
            //        //        //    }
            //        //        //    else if (herbF[herb.Key] > max2)
            //        //        //    {
            //        //        //        max2 = herbF[herb.Key];
            //        //        //    maxB = herb.Key;// + " " + herbF[herb.Key].ToString();
            //        //        //}
            //        //        //}
            //        //        showboardB.Text += prescription.Value[8]+"\r\n";
            //        //    }
            //        //}
            //        //foreach (char divide in LimitedInput)
            //        //{
            //        //    showboardB.Text = showboardB.Text.Replace(divide, '@');
            //        //}
            //        string[] zhusuindication2 = foundindicatorList.Where(s => !string.IsNullOrEmpty(s)).ToArray();
            //        zhusuindication2 = zhusuindication2.Distinct().ToArray();
            //        showboardB.Text = "";
            //        foreach (string a in zhusuindication2)
            //        {
            //            showboardB.Text += a + "\r\n";
            //        }

            //        //showboardB.Text += maxA + "\t" + maxB+"\r\n";
            //    }
            //}












            //foreach (var i in ZhongCaoYaoTCMHerbStruct)
            //{
            //    string currentHerbString = i.Value[1];
            //    string cutout = "";
            //    int start = 0;

            //    while (currentHerbString.IndexOf("功能主治", start) != -1)
            //    {
            //        int index = currentHerbString.IndexOf("功能主治", start);
            //        int end = currentHerbString.IndexOf("\r\n", index);
            //        cutout += currentHerbString.Substring(index - 1, end - index + 1);
            //        start = end;
            //    }
            //    foreach (char o in LimitedInput.ToCharArray())
            //    {
            //        cutout = cutout.Replace(o, '@');
            //    }
            //    string[] currentzhuzhilist = cutout.Split('@');
            //    foreach(string n in currentzhuzhilist)
            //    {
            //        if (!zhuzhiList.Contains(n)) zhuzhiList.Add(n);
            //    }
            //}
            //zhuzhiList = zhuzhiList.Distinct().ToList().Where(s => !string.IsNullOrEmpty(s)).ToList();
            //zhuzhiList.Sort();
            //foreach (var i in zhuzhiList)
            //{
            //    showboardA.Text += i + "\r\n";
            //}
            //WriteStreamToFile("d:\\a.txt", Encoding.UTF8.GetBytes(showboardA.Text));
            //WriteStreamToFile("d:\\TCMPrescriptionZhuzhiListStringArrya.xjt", ObjectSerialze(zhuzhiList.ToArray()));




            //List<string> HerbZhuzhi = new List<string>();
            //List<string> PrescriptionZhuzhi = new List<string>();
            ////foreach(var i in ZhongCaoYaoTCMHerbStruct)
            ////{
            ////    showboardA.Text += i.Value[3] + "\r\n";
            ////}
            ////MessageBox.Show("Herb");

            //foreach (var j in FangJiTCMPrescriptionStruct)
            //{
            //    if(j.Key== "右归丸")
            //    {
            //        string a = j.Key;
            //        MessageBox.Show("y");
            //        showboardB.Text += j.Value[8];
            //    }
            //    showboardB.Text += j.Value[8]/* + "&" + j.Value[9] + "\r\n"*/;
            //}
            //MessageBox.Show("Prescription");
            //    ///////////////方剂属性表
            //    //Dictionary<string, string[]> TCMPrescriptionStruct = new Dictionary<string, string[]>();
            //    //TCMPrescriptionStruct = (Dictionary<string, string[]>)ByteDeserialize(ReadFileToStream(@"d:\TCMPrescriptionStruct.xjt"));
            //    //MessageBox.Show(TCMPrescriptionStruct["地黄丸"][0]);
            //    //Dictionary<string, string[]> NewTCMPrescriptionStruct = new Dictionary<string, string[]>();

            //    //List<FileInfo> plist = new List<FileInfo>();
            //    //plist = GetAllFileList(@"D:\方剂", plist);
            //    //foreach (var i in plist)
            //    //{
            //    //    string pname = i.Name.Replace(".txt", "");
            //    //    string content = Encoding.UTF8.GetString(ReadFileToStream(i.FullName));
            //    //    content = pname+"~"+TCMPrescriptionStruct[pname][0] + "~"+TCMPrescriptionStruct[pname][1] + "~otherA" + "~otherB" + content.Replace("【", "~【");
            //    //    NewTCMPrescriptionStruct.Add(pname, content.Split('~'));
            //    //    NewTCMPrescriptionStruct[pname][4] = "otherB";
            //    //}





            //        ////统计booklib中每味草药提及频率
            //        //Dictionary<string, int> abc = new Dictionary<string, int>();

            //        //foreach(var j in ZhongCaoYaoTCMHerbStruct)
            //        //{
            //        //    int F = 0;
            //        //    foreach (var i in ZiLiaoJianSuoBookStruct)
            //        //    {
            //        //        int f = 0;
            //        //        string c = i.Value[2];
            //        //        c = c.Replace(j.Key, "@");
            //        //        f = c.Split('@').Count() - 1;
            //        //        F += f;

            //        //    }
            //        //    abc.Add(j.Key, F);
            //        //    newstreamcreatorpath.Text += j.Key + "@" + F.ToString() + "\r\n";
            //        //}

            //        WriteStreamToFile(@"d:\FangJiTCMPrescriptionClassPrescription.xjt", ObjectSerialze(FangJiTCMPrescriptionTypeClass));


            //    /////<button>
            //    ////< updatexjtdict >
            ////////////////////////生成方剂struct
            //List<FileInfo> plist = new List<FileInfo>();
            //Dictionary<string, string[]> np = new Dictionary<string, string[]>();
            //foreach (var a in FangJiTCMPrescriptionTypeClass)
            //{
            //    foreach (var b in a.Value)
            //    {
            //        foreach (string t in FangJiTCMPrescriptionClassPrescription[b])
            //        {
            //            string Detail = Encoding.UTF8.GetString(ReadFileToStream(@"D:\MyAllData\兴继堂\淘中医\方剂\" + t + ".txt"));
            //            string detail = Detail;
            //            detail = detail.Substring(detail.IndexOf("\r\n") + 2);
            //            detail = a.Key + "$" + b+ detail;
            //            detail = detail.Replace("【", "$【");
            //            detail = detail.Replace("\r\n\n", "\r\n");

            //            List<string> q = detail.Split('$').ToList();
            //            List<string> r = new List<string>();
            //            r.Add(t);
            //            r.Add(Detail);
            //            r.Add("attr2");
            //            r.Add("attr3");
            //            r.Add("attr4");
            //            r = r.Concat(q).ToList();
            //            np.Add(t, r.ToArray<string>());

            //        }
            //    }
            //}


            //  Dictionary<string, List<string>> xxx = new Dictionary<string, List<string>>();
            //  Dictionary<string, string[]> zzz = new Dictionary<string, string[]>();
            //foreach(var j in FangJiTCMPrescriptionStruct)
            //  {
            //      string key = j.Value[6];
            //      if (!xxx.ContainsKey(key))
            //      {
            //          List<string> tmp = new List<string>();
            //          tmp.Add(j.Key);
            //          xxx.Add(key, tmp);
            //      }
            //      else
            //      {
            //          if(!xxx[key].Contains<string>(j.Key))
            //          {
            //              xxx[key].Add(j.Key);
            //          }

            //      }

            //  }
            //  foreach(var ww in xxx)
            //  {
            //      zzz.Add(ww.Key, ww.Value.ToArray<string>());
            //  }

            //  WriteStreamToFile("d:\\TCMPrescriptionClassPrescription.xjt", ObjectSerialze(zzz));

            //    ////MessageBox.Show("finished!");
            //    //Dictionary<string, string[]> TCMTeasTypes = new Dictionary<string, string[]>();
            //    //TCMTeasTypes= (Dictionary<string, string[]>)ByteDeserialize(ReadFileToStream(@"D:\MyAllData\CSharp\TCM-Toolkit\XingjitangSuite v2.0\XingjitangSuite\bin\Debug\TCMTeasTypes.xjt"));
            //    //Dictionary<string, string> getkind = new Dictionary<string, string>();
            //    //foreach(var i in TCMTeasTypes)
            //    //{
            //    //    foreach(var j in i.Value)
            //    //    {
            //    //        if (j != "")
            //    //        {
            //    //            if (!getkind.ContainsKey(j))
            //    //            {
            //    //                getkind.Add(j, i.Key);
            //    //            }

            //    //        }

            //    //    }
            //    //}


            //    //Dictionary<string, string[]> TCMTeasStruct = new Dictionary<string, string[]>();
            //    //List<FileInfo> tealist = new List<FileInfo>();
            //    //tealist = GetAllFileList(@"D:\MyAllData\CSharp\TCM-Toolkit\XingjitangSuite v2.0\XingjitangSuite\bin\Debug\中草药茶", tealist);
            //    //foreach(var i in tealist)
            //    //{
            //    //    string teaname = i.Name.Replace(".txt", "");
            //    //    string content = Encoding.UTF8.GetString(ReadFileToStream(i.FullName));
            //    //    content = content.Substring(0, content.LastIndexOf("\r\n"));
            //    //    content = content.Replace("\r\n", "=");
            //    //    string[] teatcut = content.Split('=');
            //    //    string[] red = new string[7];
            //    //    if(teatcut.Count()== 6)
            //    //    {
            //    //        red[0] = teatcut[0];
            //    //        red[1] = teatcut[1];
            //    //        red[2] = teatcut[2];
            //    //        red[3] = teatcut[3];
            //    //        red[4] = teatcut[4];
            //    //        red[5] = teatcut[5];


            //    //    }
            //    //    else
            //    //    {
            //    //        red[0] = teatcut[0];
            //    //        red[1] = teatcut[1];
            //    //        red[2] = teatcut[2];
            //    //        red[3] = teatcut[3];
            //    //        red[4] = "用途：无记录";
            //    //        red[5] = "来源：无记录";
            //    //    }
            //    //    red[6] = getkind[teaname];

            //    //    TCMTeasStruct.Add(teaname, red);
            //    //}


            MessageBox.Show("finished!");
            //    /////</button>
        }


    }
}
