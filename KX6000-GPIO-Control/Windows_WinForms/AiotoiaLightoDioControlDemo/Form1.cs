using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenLibSys;
using System.Timers;

namespace AiotoiaLightoDioControlDemo
{
    public partial class Form1 : Form
    {
        private Ols ols; // WinRing0驱动实例
        private const int SMBUS_BASE = 0x400; // SMBUS基地址
        private const int SMBUS_STATUS = SMBUS_BASE + 0;
        private const int SMBUS_CTRL = SMBUS_BASE + 2;
        private const int SMBUS_CMD = SMBUS_BASE + 3;
        private const int SMBUS_SLV = SMBUS_BASE + 4;
        private const int SMBUS_DATA = SMBUS_BASE + 5;
        private const int F75111_SLAVE_ADR = 0x9C; // F75111芯片的I2C从设备地址

        // GPIO控制寄存器偏移量
        private const byte GPIO_SET1_DIR_OFFSET = 0x10;  // GPIO方向寄存器偏移量
        private const byte GPIO_SET2_DIR_OFFSET = 0x20;  // GPIO方向寄存器偏移量
        private const byte GPIO_SET1_OUTPUT_DATA_OFFSET = 0x11; // GPIO输出数据寄存器偏移量
        private const byte GPIO_SET2_OUTPUT_DATA_OFFSET = 0x21; // GPIO输出数据寄存器偏移量
        private const byte GPIO_SET1_INPUT_DATA_OFFSET = 0x12; // GPIO输入数据寄存器偏移量
        private const byte GPIO_SET2_INPUT_DATA_OFFSET = 0x22; // GPIO输入数据寄存器偏移量

        private System.Timers.Timer timer; // 定时器，用于定时器检测输入电平
        private byte currentOutputData1 = 0x00; // 当前输出数据1
        private byte currentOutputData2 = 0x00; // 当前输出数据2
        private bool isInputMode = true; // 默认为输入模式

        // 映射 CheckBox 到 GPIO 位
        private Dictionary<CheckBox, (byte offset, byte bit)> checkBoxToGpioMap;

        public Form1()
        {
            InitializeComponent();
            InitializeCheckBoxMap(); // 初始化 CheckBox 映射
            InitializeDriver(); // 初始化驱动
            InitializeGpio();  // 初始化GPIO为输入模式
            InitializeTimer(); // 初始化定时器

            timer.Elapsed += new ElapsedEventHandler(Timer_Elapsed);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);

            // 订阅所有复选框的CheckedChanged事件
            foreach (var checkBox in checkBoxToGpioMap.Keys)
            {
                checkBox.CheckedChanged += checkBox_CheckedChanged;
            }
            // 新增按钮事件
            button1.Click += Button1_Click; // 切换到输入模式
            button2.Click += Button2_Click; // 切换到输出模式
            button3.Click += Button3_Click; // 所有端口输出高电平
            button4.Click += Button4_Click; // 所有端口输出低电平
        }

        //初始化CheckBox映射
        private void InitializeCheckBoxMap()
        {
            checkBoxToGpioMap = new Dictionary<CheckBox, (byte, byte)>
            {
                { checkBox1, (GPIO_SET1_OUTPUT_DATA_OFFSET, 0) }, // checkBox1 对应端口1，Bit0
                { checkBox2, (GPIO_SET1_OUTPUT_DATA_OFFSET, 1) }, // checkBox2 对应端口2，Bit1
                { checkBox3, (GPIO_SET1_OUTPUT_DATA_OFFSET, 2) }, // checkBox3 对应端口3，Bit2
                { checkBox4, (GPIO_SET1_OUTPUT_DATA_OFFSET, 4) }, // checkBox4 对应端口4，Bit4
                { checkBox5, (GPIO_SET1_OUTPUT_DATA_OFFSET, 6) }, // checkBox5 对应端口5，Bit6
                { checkBox6, (GPIO_SET1_OUTPUT_DATA_OFFSET, 7) }, // checkBox6 对应端口6，Bit7
                { checkBox7, (GPIO_SET2_OUTPUT_DATA_OFFSET, 0) }, // checkBox7 对应端口7，Bit0
                { checkBox8, (GPIO_SET2_OUTPUT_DATA_OFFSET, 1) }, // checkBox8 对应端口8，Bit1
            };
        }

        private void InitializeDriver()
        {
            ols = new Ols(); // 创建Ols实例，用于WinRing0驱动重载
            uint status = ols.GetStatus();
            if (status != (uint)Ols.Status.NO_ERROR)
            {
                string errorMessage = GetErrorMessage(status);
                MessageBox.Show("驱动加载失败！错误代码：" + status + "\n错误信息：" + errorMessage);
            }
            else
            {
                MessageBox.Show("驱动加载成功！");
            }
        }

        private string GetErrorMessage(uint status)
        {
            switch (status)
            {
                case (uint)Ols.Status.DLL_NOT_FOUND:
                    return "动态链接库(WinRing0)未找到。";
                case (uint)Ols.Status.DLL_INCORRECT_VERSION:
                    return "WinRing0版本不正确，请检查WinRing0的项目链接库文件是否是定制版本。";
                case (uint)Ols.Status.DLL_INITIALIZE_ERROR:
                    return "驱动在初始化过程中找不到相关硬体接口，初始化失败，请检查运行机器是否是本项目匹配的光源和DIO模块的目标主机。";
                default:
                    return "未知错误。";
            }
        }

        //初始化GPIO
        private void InitializeGpio()
        {
            // 默认设置为输入模式
            WriteSmbusData(GPIO_SET1_DIR_OFFSET, 0x00); // 所有GPIO引脚都设置为输入
            WriteSmbusData(GPIO_SET2_DIR_OFFSET, 0x00); // 所有GPIO引脚都设置为输入
        }

        //写入SMBUS数据
        private void WriteSmbusData(byte offset, byte data)
        {
            ols.WriteIoPortByte(SMBUS_STATUS, 0x42); // 清除状态
            ols.WriteIoPortByte(SMBUS_CMD, offset);  //设置目标寄存器偏移量
            ols.WriteIoPortByte(SMBUS_SLV, F75111_SLAVE_ADR);//设置写模式地址
            ols.WriteIoPortByte(SMBUS_DATA, data);//写入数据
            ols.WriteIoPortByte(SMBUS_CTRL, 0x48); // 字节访问
            System.Threading.Thread.Sleep(1); // 等待一段时间以确保操作完成
        }

        //读取SMBUS数据
        private byte ReadSmbusData(byte offset)
        {           
            ols.WriteIoPortByte(SMBUS_STATUS, 0x42);// 清除状态
            ols.WriteIoPortByte(SMBUS_CMD, offset);//设置目标寄存器偏移量
            ols.WriteIoPortByte(SMBUS_SLV, (byte)(F75111_SLAVE_ADR | 0x01));// 设置读模式地址 (0x9C | BIT0 = 0x9D)           
            ols.WriteIoPortByte(SMBUS_CTRL, 0x48);//触发字节读操作（控制码与写操作相同，但地址为读模式）
            System.Threading.Thread.Sleep(2); //  等待操作完成           
            return ols.ReadIoPortByte(SMBUS_DATA); // 读取数据
        }

        private void InitializeTimer()
        {
            timer = new System.Timers.Timer(200); // 设置定时器间隔为200毫秒
            timer.Elapsed += Timer_Elapsed; // 设置定时器回调函数
            timer.Start(); // 启动定时器
        }

        //定时器回调函数
        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (isInputMode)
            {
                UpdateInputLabels();//更新输入标签
            }
        }

        //更新输入标签
        private void UpdateInputLabels()
        {
            this.Invoke((MethodInvoker)delegate
            {
                byte inputData1 = ReadSmbusData(GPIO_SET1_INPUT_DATA_OFFSET);//读取SET1输入数据
                byte inputData2 = ReadSmbusData(GPIO_SET2_INPUT_DATA_OFFSET);//读取SET2输入数据

                // 处理 SET1 的 GPIO
                UpdateLabelState("label1", (inputData1 & (1 << 0)) != 0);  // Bit0
                UpdateLabelState("label2", (inputData1 & (1 << 1)) != 0);  // Bit1
                UpdateLabelState("label3", (inputData1 & (1 << 2)) != 0);  // Bit2
                UpdateLabelState("label4", (inputData1 & (1 << 4)) != 0);  // Bit4
                UpdateLabelState("label5", (inputData1 & (1 << 6)) != 0);  // Bit6
                UpdateLabelState("label6", (inputData1 & (1 << 7)) != 0);  // Bit7

                // 处理 SET2 的 GPIO
                UpdateLabelState("label7", (inputData2 & (1 << 0)) != 0); // Bit0
                UpdateLabelState("label8", (inputData2 & (1 << 1)) != 0); // Bit1
            });
        }

        //更新标签状态
        private void UpdateLabelState(string labelName, bool isHigh)
        {
            Label label = this.Controls.Find(labelName, true).FirstOrDefault() as Label;
            if (label != null)
            {
                if (isInputMode)
                {
                    label.Text = isHigh ? "H" : "L";
                    label.BackColor = isHigh ? Color.Red : Color.Green; // 设置背景颜色
                }
                else
                {
                    label.Text = ""; // 在非输入模式下清空标签
                    label.BackColor = SystemColors.Control; // 恢复默认背景颜色
                }
            }
        }

        //复选框状态改变事件
        private void checkBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!isInputMode)//只有在输出模式下才处理CheckBox的变化
            {
                CheckBox checkBox = (CheckBox)sender;
                if (checkBoxToGpioMap.TryGetValue(checkBox, out var gpioInfo))
                {
                    byte offset = gpioInfo.offset;
                    byte bit = gpioInfo.bit;

                    if (offset == GPIO_SET1_OUTPUT_DATA_OFFSET)
                    {
                        currentOutputData1 = checkBox.Checked ?
                            (byte)(currentOutputData1 | (1 << bit)) :
                            (byte)(currentOutputData1 & ~(1 << bit));
                        WriteSmbusData(offset, currentOutputData1);
                    }
                    else if (offset == GPIO_SET2_OUTPUT_DATA_OFFSET)
                    {
                        currentOutputData2 = checkBox.Checked ?
                            (byte)(currentOutputData2 | (1 << bit)) :
                            (byte)(currentOutputData2 & ~(1 << bit));
                        WriteSmbusData(offset, currentOutputData2);
                    }
                }
            }
        }

        //"开始输入"按钮点击事件
        private void Button1_Click(object sender, EventArgs e)
        {
            //将所有引脚全部切换成输入模式，因芯片特性，输入模式会导致全部默认为高电平状态【因芯片特性是上拉输入】，需要在执行一次低电平切换操作
            WriteSmbusData(GPIO_SET1_DIR_OFFSET, 0x00);
            WriteSmbusData(GPIO_SET2_DIR_OFFSET, 0x00);
            isInputMode = true;

            WriteSmbusData(GPIO_SET1_DIR_OFFSET, 0xFF);
            WriteSmbusData(GPIO_SET2_DIR_OFFSET, 0xFF);
            //低电平状态切换，需要调用button4的点击逻辑
            currentOutputData1 = 0x00;
            currentOutputData2 = 0x00;
            WriteSmbusData(GPIO_SET1_OUTPUT_DATA_OFFSET, currentOutputData1);
            WriteSmbusData(GPIO_SET2_OUTPUT_DATA_OFFSET, currentOutputData2);

            foreach (var checkBox in checkBoxToGpioMap.Keys)
            {
                var (offset, bit) = checkBoxToGpioMap[checkBox];
                checkBox.Checked = (offset == GPIO_SET1_OUTPUT_DATA_OFFSET) ?
                    (currentOutputData1 & (1 << bit)) != 0 :
                    (currentOutputData2 & (1 << bit)) != 0;
            }

            // 禁用并重置复选框
            foreach (var checkBox in checkBoxToGpioMap.Keys)
            {
                checkBox.Enabled = false;
                checkBox.Checked = false;
            }

            MessageBox.Show("GPIO已切换为输入模式");
        }

        //“开始输出”按钮点击事件
        private void Button2_Click(object sender, EventArgs e)
        {
            WriteSmbusData(GPIO_SET1_DIR_OFFSET, 0xFF);
            WriteSmbusData(GPIO_SET2_DIR_OFFSET, 0xFF);
            isInputMode = false;
            ClearLabels();

            // 启用复选框并更新状态
            foreach (var checkBox in checkBoxToGpioMap.Keys)
            {
                checkBox.Enabled = true;
                var (offset, bit) = checkBoxToGpioMap[checkBox];
                bool isChecked = (offset == GPIO_SET1_OUTPUT_DATA_OFFSET) ?
                    (currentOutputData1 & (1 << bit)) != 0 :
                    (currentOutputData2 & (1 << bit)) != 0;
                checkBox.Checked = isChecked;
            }

            MessageBox.Show("GPIO已切换为输出模式");
        }

        //清空label所有标签
        private void ClearLabels()
        {
            ClearLabelsRecursive(this);
        }

        //递归清空所有标签
        private void ClearLabelsRecursive(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is Label label)
                {
                    label.Text = "";
                    label.BackColor = SystemColors.Control;
                }
                else if (control.HasChildren)
                {
                    ClearLabelsRecursive(control);
                }
            }
        }

        //全部输出高电平按钮
        private void Button3_Click(object sender, EventArgs e)
        {
            currentOutputData1 = 0xFF;
            currentOutputData2 = 0xFF;
            WriteSmbusData(GPIO_SET1_OUTPUT_DATA_OFFSET, currentOutputData1);
            WriteSmbusData(GPIO_SET2_OUTPUT_DATA_OFFSET, currentOutputData2);

            //更新复选框状态
            foreach (var checkBox in checkBoxToGpioMap.Keys)
            {
                var (offset, bit) = checkBoxToGpioMap[checkBox];
                checkBox.Checked = (offset == GPIO_SET1_OUTPUT_DATA_OFFSET) ?
                    (currentOutputData1 & (1 << bit)) != 0 :
                    (currentOutputData2 & (1 << bit)) != 0;
            }
        }

        //全部输出低电平状态
        private void Button4_Click(object sender, EventArgs e)
        {
            currentOutputData1 = 0x00;
            currentOutputData2 = 0x00;
            WriteSmbusData(GPIO_SET1_OUTPUT_DATA_OFFSET, currentOutputData1);
            WriteSmbusData(GPIO_SET2_OUTPUT_DATA_OFFSET, currentOutputData2);

            //更新复选框状态
            foreach (var checkBox in checkBoxToGpioMap.Keys)
            {
                var (offset, bit) = checkBoxToGpioMap[checkBox];
                checkBox.Checked = (offset == GPIO_SET1_OUTPUT_DATA_OFFSET) ?
                    (currentOutputData1 & (1 << bit)) != 0 :
                    (currentOutputData2 & (1 << bit)) != 0;
            }
        }

        //窗口关闭事件
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            timer.Stop(); // 停止定时器

            MessageBox.Show("程序和驱动将释放，各接口将会被设置为NULL状态");
            if (ols != null)
            {
                ols.DeinitializeOls();
                ols.Dispose();
            }
        }
    }
}