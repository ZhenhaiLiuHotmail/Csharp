using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace SearchTool
{
    public partial class MainForm : Form
    {
        protected string filePath = string.Empty;
        public MainForm()
        {
            InitializeComponent();
        }

        private void btnFileUpload_Click(object sender, EventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Filter = "(*.txt)|*.txt";
            if (fileDialog.ShowDialog() == DialogResult.OK)
            {
                string extension = Path.GetExtension(fileDialog.FileName);
                string[] arrStr = new string[] { ".txt" };
                if (!((IList)arrStr).Contains(extension))
                {
                    MessageBox.Show("仅能上传txt格式的文件！");
                }
                else
                {
                    //FileInfo fileInfo = new FileInfo(fileDialog.FileName);
                    this.lblFilePath.Text = fileDialog.FileName;
                    this.filePath = fileDialog.FileName;
                }

                this.txtResult.Text = "";
            }
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(this.filePath))
            {
                MessageBox.Show("请先选择源文件！");
                return;
            }
            if (string.IsNullOrEmpty(this.txtKeyword.Text))
	        {
                MessageBox.Show("请输入搜索关键词！");
                return;
	        }

            string line = string.Empty;
            int rowCount = 0;
            StringBuilder sb = new StringBuilder();
            StreamReader srFile = null;
            try
            {
                //srFile = new StreamReader(this.filePath, System.Text.Encoding.Default);
                srFile = new StreamReader(this.filePath);
                while ((line = srFile.ReadLine()) != null)
                {
                    if (line.IndexOf(this.txtKeyword.Text) >= 0)
                    {
                        rowCount++;
                        sb.AppendLine(line);   
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                if (srFile != null)
                {
                    srFile.Close();
                }
            }

            this.txtResult.Text = sb.ToString();
            this.txtKeyword.Focus();
            //MessageBox.Show(string.Format("{0}个结果", rowCount.ToString()));
        }
    }
}
