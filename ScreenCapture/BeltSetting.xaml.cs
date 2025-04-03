﻿using System;
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
using System.Windows.Shapes;

namespace WPFCaptureSample
{
    /// <summary>
    /// BeltSetting.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class BeltSetting : Window
    {
        public BeltSetting()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txt_Hero.Text = Properties.Settings.Default.HeroNum;
            txt_Bag.Text = Properties.Settings.Default.BagNum;
            cbb_BeltNum.Text = Properties.Settings.Default.BeltNum;
            txt_BeltSpeed.Text = Properties.Settings.Default.BeltSpeed;
        }

        private void btn_beltSave_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txt_Hero.Text, out _) || !int.TryParse(txt_Bag.Text, out _))
            {
                MessageBox.Show("영웅과 창고는 숫자만 입력해주세요.");
                return;
            }

            if (!double.TryParse(txt_BeltSpeed.Text, out _))
            {
                MessageBox.Show("벨트 속도는 숫자(초)로 입력해주세요.");
                return;
            }
            BeltSetting_Save();
            this.Close();
        }

        private void BeltSetting_Save()
        {
            Properties.Settings.Default.HeroNum = txt_Hero.Text;
            Properties.Settings.Default.BagNum = txt_Bag.Text;
            Properties.Settings.Default.BeltNum = cbb_BeltNum.Text;
            Properties.Settings.Default.BeltSpeed = txt_BeltSpeed.Text;
            Properties.Settings.Default.Save();
        }

        private void btn_beltClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
