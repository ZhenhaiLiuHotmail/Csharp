using System;
using System.Collections.Generic;
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
using System.Management;
using System.Xml.Serialization;
using System.Security.Cryptography;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace EnvironmentSerialNumber
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        string SUITEWORKROOT = System.Environment.CurrentDirectory + "\\";
        byte[] MYIV = new byte[16];
        byte[] MYKEY = new byte[32];
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

        string[] LicenceInfo = {"","",""};

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

        public MainWindow()
        {
            InitializeComponent();
        }

        private void GetIDBT_Click(object sender, RoutedEventArgs e)
        {
            LicenceInfo[0] = EnvironmentSerialNumber()[0];
            LicenceInfo[1] = EnvironmentSerialNumber()[1];
            DiskDriveSerialNumbersTB.Text = LicenceInfo[0];
            TargetIDTB.Text = "";
            biostb.Text = LicenceInfo[1];
            biostb_en.Text = Encoding.UTF8.GetString(EncryptStringToBytes_Aes(Encoding.UTF8.GetBytes(LicenceInfo[1]), MYKEY, MYIV));
        }

        private void NewFileBT_Click(object sender, RoutedEventArgs e)
        {
            LicenceInfo[0] = TargetIDTB.Text;
            LicenceInfo[1] = biostb.Text;
            LicenceInfo[2] = InvalidDateTB.Text;
            if(File.Exists(SUITEWORKROOT + "0.xjt")) { File.Delete(SUITEWORKROOT + "0.xjt"); }
            MessageBox.Show("准备生成：\r\n"+ SUITEWORKROOT + "0.xjt"+"\r\n目标驱动器："+TargetIDTB.Text);
            WriteStreamToFile(SUITEWORKROOT + "0.xjt", EncryptStringToBytes_Aes(ObjectSerialze(LicenceInfo), MYKEY, MYIV));
        }

        private void LoadBT_Click(object sender, RoutedEventArgs e)
        {
            LicenceInfo = (String[])ByteDeserialize(DecryptStringFromBytes_Aes(ReadFileToStream(LoadpathTB.Text), MYKEY, MYIV));
            DiskDriveSerialNumbersTB.Text = EnvironmentSerialNumber()[0];
            TargetIDTB.Text = LicenceInfo[0];
            biostb.Text = LicenceInfo[1];
            biostb_en.Text = Encoding.UTF8.GetString(EncryptStringToBytes_Aes(Encoding.UTF8.GetBytes(LicenceInfo[1]), MYKEY, MYIV));
            InvalidDateTB.Text = LicenceInfo[2];
        }

        private void EncrptBT_Click(object sender, RoutedEventArgs e)
        {
            if (TargetIDTB.Text.Length < 32)
            {
                string encriptRoot = EncriptPath.Text;
                MessageBox.Show("准备启动加密过程！\r\n密钥明文：" + TargetIDTB.Text);
                List<FileInfo> allFiles = new List<FileInfo>();
                allFiles = GetAllFileList(encriptRoot, allFiles);
                foreach (FileInfo currentFile in allFiles)
                {
                    byte[] encriptedStream = EncryptStringToBytes_Aes(ReadFileToStream(currentFile.FullName), EncryptStringToBytes_Aes(Encoding.UTF8.GetBytes(TargetIDTB.Text), MYKEY, MYIV), MYIV);
                    File.Delete(currentFile.FullName);
                    WriteStreamToFile(currentFile.FullName, encriptedStream);
                }
                MessageBox.Show("Encription finished!");
            }
            else
            {
                MessageBox.Show("mykey超出32位！");
            }

        }
    }
}
