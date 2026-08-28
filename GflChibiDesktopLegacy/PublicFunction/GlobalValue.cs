using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class GlobalValue : INotifyPropertyChanged
{
    private string _SelectAtlasFile = string.Empty;
    private string _SelectSpineFile = string.Empty;
    private string _SpineVersion = string.Empty;
    private List<string> _AnimeList;
    private List<string> _SkinList;
    private float _Scale;
    private double _ViewScale;
    private int _Speed = 30;
    private float _PosX = 0;
    private float _PosY = 0;
    private float _PosBGX = 0;
    private float _PosBGY = 0;
    private bool _UseBG = false;
    private string _SelectBG = string.Empty;
    private bool _ControlBG = false;
    private bool _Alpha;
    private bool _IsLoop;
    private bool _EnableInteraction;
    private bool _Simulation;
    private bool _Simulation_Moving;
    private bool _IsDormMode;
    private float _AnimeDuration;
    private string _SelectAnimeName = string.Empty;
    private string _SelectedAnime = string.Empty;
    private string[] _DummyTag;
    private string _SelectSkin = string.Empty;
    private float _TimeScale = 1;
    private string _SelectSpineVersion = string.Empty;
    private double _FrameWidth;
    private double _FrameHeight;
    private double _Opacity;
    private bool _PreMultiplyAlpha;
    private bool _SetSkin = false;
    private bool _SetAnime = false;
    private string _FileHash = string.Empty;
    private string _GifQuality = "Default";
    private string _LoadingProcess = "0%";
    private string _Dummy = string.Empty;
    private string _DummyDisplayName = "未加载模型";
    private float _Lock = 0f;
    private bool _IsRecoding = false;
    private bool _FilpX = false;
    private bool _FilpY = false;
    private float _RedcodePanelWidth = 280f;
    private float _Rotation = 0;
    private bool _UseCache = false;
    private bool _StartupLaunch = false;

    private string _DownloadSource = string.Empty;

    private System.Windows.Media.Brush _CanvasBackground;

    private List<Texture2D> _GifList;


    public string SelectAtlasFile
    {
        get
        {
            return _SelectAtlasFile;
        }
        set
        {
            if (_SelectAtlasFile != value)
            {
                _SelectAtlasFile = value;
                OnPropertyChanged("SelectAtlasFile");
            }
        }
    }

    public string SelectSpineFile
    {
        get
        {
            return _SelectSpineFile;
        }
        set
        {
            if (_SelectSpineFile != value)
            {
                _SelectSpineFile = value;
                OnPropertyChanged("SelectSpineFile");
            }
        }
    }

    public string SpineVersion
    {
        get
        {
            return _SpineVersion;
        }
        set
        {
            if (_SpineVersion != value)
            {
                _SpineVersion = value;
                OnPropertyChanged("SpineVersion");
            }
        }
    }


    public List<string> AnimeList
    {
        get
        {
            return _AnimeList;
        }
        set
        {
            if (_AnimeList != value)
            {
                _AnimeList = value;
                OnPropertyChanged("AnimeList");
            }
        }
    }
    public List<string> SkinList
    {
        get
        {
            return _SkinList;
        }
        set
        {
            if (_SkinList != value)
            {
                _SkinList = value;
                OnPropertyChanged("SkinList");
            }
        }
    }
    public float Scale
    {
        get
        {
            return _Scale;
        }
        set
        {
            if (_Scale != value)
            {
                _Scale = (float)Math.Round(value, 2);
                OnPropertyChanged("Scale");
            }
        }
    }
    public double ViewScale
    {
        get
        {
            return _ViewScale;
        }
        set
        {
            if (_ViewScale != value)
            {
                _ViewScale = (double)Math.Round(value, 2);
                OnPropertyChanged("ViewScale");
            }
        }
    }

    public int Speed
    {
        get
        {
            return _Speed;
        }
        set
        {
            if (int.TryParse(value.ToString(), out _Speed))
            {
                if (_Speed != value)
                {
                    _Speed = value;
                    OnPropertyChanged("Speed");
                }
            }
        }
    }
    public float PosX
    {
        get
        {
            return _PosX;
        }
        set
        {
            if (_PosX != value)
            {
                _PosX = (float)Math.Round(value, 2);
                OnPropertyChanged("PosX");
            }
        }
    }
    public float PosY
    {
        get
        {
            return _PosY;
        }
        set
        {
            if (_PosY != value)
            {
                _PosY = (float)Math.Round(value, 2);
                OnPropertyChanged("PosY");
            }
        }
    }

    public float PosBGX
    {
        get
        {
            return _PosBGX;
        }
        set
        {
            if (_PosBGX != value)
            {
                _PosBGX = (float)Math.Round(value, 2);
                OnPropertyChanged("PosBGX");
            }
        }
    }
    public float PosBGY
    {
        get
        {
            return _PosBGY;
        }
        set
        {
            if (_PosBGY != value)
            {
                _PosBGY = (float)Math.Round(value, 2);
                OnPropertyChanged("PosBGY");
            }
        }
    }
    public bool Alpha
    {
        get
        {
            return _Alpha;
        }
        set
        {
            if (_Alpha != value)
            {
                _Alpha = value;
                OnPropertyChanged("Alpha");
            }
        }
    }
    public bool UseBG
    {
        get
        {
            return _UseBG;
        }
        set
        {
            if (_UseBG != value)
            {
                _UseBG = value;
                OnPropertyChanged("UseBG");
            }
        }
    }
    public bool ControlBG
    {
        get
        {
            return _ControlBG;
        }
        set
        {
            if (_ControlBG != value)
            {
                _ControlBG = value;
                OnPropertyChanged("ControlBG");
            }
        }
    }
    public bool IsLoop
    {
        get
        {
            return _IsLoop;
        }
        set
        {
            if (_IsLoop != value)
            {
                _IsLoop = value;
                OnPropertyChanged("IsLoop");
            }
        }
    }
    public bool EnableInteraction
    {
        get
        {
            return _EnableInteraction;
        }
        set
        {
            if (_EnableInteraction != value)
            {
                _EnableInteraction = value;
                OnPropertyChanged("EnableInteraction");
            }
        }
    }

    public bool Simulation
    {
        get
        {
            return _Simulation;
        }
        set
        {
            if (_Simulation != value)
            {
                _Simulation = value;
                OnPropertyChanged("Simulation");
            }
        }
    }
    public bool Simulation_Moving
    {
        get
        {
            return _Simulation_Moving;
        }
        set
        {
            if (_Simulation_Moving != value)
            {
                _Simulation_Moving = value;
                OnPropertyChanged("Simulation_Moving");
            }
        }
    }

    public string SelectAnimeName
    {
        get
        {
            return _SelectAnimeName;
        }
        set
        {
            if (_SelectAnimeName != value)
            {
                _SelectAnimeName = value;
                OnPropertyChanged("SelectAnimeName");
            }
        }
    }
    public string SelectedAnime
    {
        get
        {
            return _SelectedAnime;
        }
        set
        {
            if (_SelectedAnime != value)
            {
                _SelectedAnime = value;
                OnPropertyChanged("SelectedAnime");
            }
        }
    }
    public string[] DummyTag
    {
        get
        {
            return _DummyTag;
        }
        set
        {
            if (_DummyTag != value)
            {
                _DummyTag = value;
                OnPropertyChanged("DummyTag");
            }
        }
    }
    public float AnimeDuration
    {
        get
        {
            return _AnimeDuration;
        }
        set
        {
            if (_AnimeDuration != value)
            {
                _AnimeDuration = value;
                OnPropertyChanged("AnimeDuration");
            }
        }
    }
    public string SelectSkin
    {
        get
        {
            return _SelectSkin;
        }
        set
        {
            if (_SelectSkin != value)
            {
                _SelectSkin = value;
                OnPropertyChanged("SelectSkin");
            }
        }
    }
    public string SelectBG
    {
        get
        {
            return _SelectBG;
        }
        set
        {
            if (_SelectBG != value)
            {
                _SelectBG = value;
                OnPropertyChanged("SelectBG");
            }
        }
    }

    public float TimeScale
    {
        get
        {
            return _TimeScale;
        }
        set
        {
            if (_TimeScale != value)
            {
                _TimeScale = value;
                OnPropertyChanged("TimeScale");
            }
        }
    }
    public string SelectSpineVersion
    {
        get
        {
            return _SelectSpineVersion;
        }
        set
        {
            if (_SelectSpineVersion != value)
            {
                _SelectSpineVersion = value;
                OnPropertyChanged("SelectSpineVersion");
            }
        }
    }
    public double FrameWidth
    {
        get
        {
            return _FrameWidth;
        }
        set
        {
            if (_FrameWidth != value)
            {
                _FrameWidth = value;
                OnPropertyChanged("FrameWidth");
            }
        }
    }
    public double FrameHeight
    {
        get
        {
            return _FrameHeight;
        }
        set
        {
            if (_FrameHeight != value)
            {
                _FrameHeight = value;
                OnPropertyChanged("FrameHeight");
            }
        }
    }

    public double Opacity
    {
        get
        {
            return _Opacity;
        }
        set
        {
            if (_Opacity != value)
            {
                _Opacity = value;
                OnPropertyChanged("Opacity");
            }
        }
    }

    public bool PreMultiplyAlpha
    {
        get
        {
            return _PreMultiplyAlpha;
        }
        set
        {
            if (_PreMultiplyAlpha != value)
            {
                _PreMultiplyAlpha = value;
                OnPropertyChanged("PreMultiplyAlpha");
            }
        }
    }

    public bool SetSkin
    {
        get
        {
            return _SetSkin;
        }
        set
        {
            if (_SetSkin != value)
            {
                _SetSkin = value;
                OnPropertyChanged("SetSkin");
            }
        }
    }

    public bool SetAnime
    {
        get
        {
            return _SetAnime;
        }
        set
        {
            if (_SetAnime != value)
            {
                _SetAnime = value;
                OnPropertyChanged("SetAnime");
            }
        }
    }

    public string FileHash
    {
        get
        {
            return _FileHash;
        }
        set
        {
            if (_FileHash != value)
            {
                _FileHash = value;
                OnPropertyChanged("FileHash");
            }
        }
    }

    public string GifQuality
    {
        get
        {
            return _GifQuality;
        }
        set
        {
            if (_GifQuality != value)
            {
                _GifQuality = value;
                OnPropertyChanged("GifQuality");
            }
        }
    }
    public string LoadingProcess
    {
        get
        {
            return _LoadingProcess;
        }
        set
        {
            if (_LoadingProcess != value)
            {
                _LoadingProcess = value;
                OnPropertyChanged("LoadingProcess");
            }

        }
    }

    public float Lock
    {
        get
        {
            return _Lock;
        }
        set
        {
            if (float.TryParse(value.ToString(), out _Lock))
            {
                _Lock = (float)Math.Round(value, 2);
                OnPropertyChanged("Lock");
            }
        }
    }

    public List<Texture2D> GifList
    {

        get
        {
            if (_GifList == null)
                _GifList = new List<Texture2D>();


            return _GifList;
        }
        set
        {
            if (_GifList != value)
            {
                _GifList = value;
            }
           

        }
    }

    public bool IsRecoding
    {
        get
        {
            return _IsRecoding;
        }
        set
        {
            if (_IsRecoding != value)
            {
                _IsRecoding = value;
                OnPropertyChanged("IsRecoding");
            }
        }
    }

    public bool FilpX
    {
        get
        {
            return _FilpX;
        }
        set
        {
            if (_FilpX != value)
            {
                _FilpX = value;
                OnPropertyChanged("FilpX");
            }
        }
    }

    public bool FilpY
    {
        get
        {
            return _FilpY;
        }
        set
        {
            if (_FilpY != value)
            {
                _FilpY = value;
                OnPropertyChanged("FilpY");
            }
        }
    }

    public bool IsDormMode
    {
        get
        {
            return _IsDormMode;
        }
        set
        {
            if (_IsDormMode != value)
            {
                _IsDormMode = value;
                OnPropertyChanged("IsDormMode");
            }
        }
    }

    public float RedcodePanelWidth
    {
        get
        {
            return _RedcodePanelWidth;
        }
        set
        {
            if (float.TryParse(value.ToString(), out _RedcodePanelWidth))
            {
                if (_RedcodePanelWidth != value)
                {
                    _RedcodePanelWidth = value;
                    OnPropertyChanged("RedcodePanelWidth");
                }
            }
        }
    }

    public float Rotation
    {
        get
        {
            return _Rotation;
        }
        set
        {
            if (float.TryParse(value.ToString(), out _Rotation))
            {
                if (_Rotation != value)
                {
                    _Rotation = value;
                    OnPropertyChanged("Rotation");
                }
            }
        }
    }

    public bool UseCache
    {
        get
        {
            return _UseCache;
        }
        set
        {
            if (_UseCache != value)
            {
                _UseCache = value;
                OnPropertyChanged("UseCache");
            }
        }
    }

    public bool StartupLaunch
    {
        get
        {
            return _StartupLaunch;
        }
        set
        {
            if (_StartupLaunch != value)
            {
                _StartupLaunch = value;
                OnPropertyChanged("StartupLaunch");
            }
        }
    }

    public string Dummy
    {
        get
        {
            return _Dummy;
        }
        set
        {
            if (_Dummy != value)
            {
                _Dummy = value;
                OnPropertyChanged("Dummy");
            }
        }
    }
    public string DummyDisplayName
    {
        get
        {
            return _DummyDisplayName;
        }
        set
        {
            if (_DummyDisplayName != value)
            {
                _DummyDisplayName = value;
                OnPropertyChanged("DummyDisplayName");
            }
        }
    }

    public string DownloadSource
    {
        get
        {
            return _DownloadSource;
        }
        set
        {
            if (_DownloadSource != value)
            {
                _DownloadSource = value;
                OnPropertyChanged("DownloadSource");
            }
        }
    }
    public System.Windows.Media.Brush CanvasBackground
    {
        get
        {
            return _CanvasBackground;
        }
        set
        {
            if (_CanvasBackground != value)
            {
                _CanvasBackground = value;
                OnPropertyChanged("CanvasBackground");
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    public void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }


}

