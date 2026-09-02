using Microsoft.Win32;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using GflChibiDesktop;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;

public class Common
{
    public static string GetDirName(string path)
    {
        return Path.GetDirectoryName(path);
    }

    public static string GetFileNameNoEx(string path)
    {
        return Path.GetFileNameWithoutExtension(path);
    }

    public static bool IsBinaryData(string path)
    {
        if (File.Exists(path.Replace(".atlas", ".skel")) && path.IndexOf(".skel") > -1)
            return true;
        else
            return false;
    }

    public static bool CheckSpineFile(string path)
    {
        if (File.Exists(path.Replace(".atlas", ".skel")))
        {

            App.globalValues.SelectSpineFile = path.Replace(".atlas", ".skel");
            return true;
        }
        else if (File.Exists(path.Replace(".atlas", ".json")))
        {
            App.globalValues.SelectSpineFile = path.Replace(".atlas", ".json");
            return true;
        }
        else
        {
            App.globalValues.SelectSpineFile = string.Empty;
            return false;
        }
          
    }


    public static string GetSkelPath(string path)
    {
        return path.Replace(".atlas", ".skel");
    }

    public static string GetJsonPath(string path)
    {
        return path.Replace(".atlas", ".json");
    }

    public static void SetXY(double MosX, double MosY, double oldX, double oldY)
    {
        App.globalValues.PosX = (float)(MosX + App.globalValues.PosX - oldX);
        App.globalValues.PosY = (float)(MosY + App.globalValues.PosY - oldY);
    }

    public static void SetBGXY(double MosX, double MosY, double oldX, double oldY)
    {
        App.globalValues.PosBGX = (float)(MosX + App.globalValues.PosBGX - oldX);
        App.globalValues.PosBGY = (float)(MosY + App.globalValues.PosBGY - oldY);
    }

    public static void SetInitLocation(float height)
    {
        //if (App.isNew)
        //{
        //    //App.globalValues.PosX = Convert.ToSingle(App.globalValues.FrameWidth / 2f);
        //    //App.globalValues.PosY = Convert.ToSingle((height + App.globalValues.FrameHeight) / 2f);
        //    App.globalValues.PosX = 224;
        //    App.globalValues.PosY = 224;
        //}
    }

}


