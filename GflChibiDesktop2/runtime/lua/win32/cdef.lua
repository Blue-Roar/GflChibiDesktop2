local ffi = require("ffi")

ffi.cdef [[
    typedef struct tagRECT {
        uint32_t left;
        uint32_t top;
        uint32_t right;
        uint32_t bottom;
    } RECT;
    typedef struct tagPOINT {
        long x;
        long y;
    } POINT;
    typedef struct _BLENDFUNCTION {
        char BlendOp;
        char BlendFlags;
        char SourceConstantAlpha;
        char AlphaFormat;
    } BLENDFUNCTION, *PBLENDFUNCTION;

    uint32_t GetForegroundWindow();
    uint32_t GetWindowRect(uint32_t hWnd, RECT* lpRect);
    uint32_t GetDesktopWindow();
    uint32_t GetShellWindow();
    void Sleep(uint32_t time);
    uint32_t GetClassNameA(uint32_t hwnd, char* className, uint32_t n);
    uint32_t GetParent(uint32_t hwnd);
    bool GetCursorPos(POINT* lpPoint);
    long GetWindowLongW(uint32_t hWnd, int nIndex);
    long SetWindowLongW(uint32_t hWnd, int nIndex, long dwNewLong);
    long GetLastError();
    uint32_t SetCapture(uint32_t hWnd);
    int ReleaseCapture();
    void SetForegroundWindow(uint32_t hWnd);
    bool SetWindowPos(uint32_t hWnd, uint32_t hWndInsertAfter, int X, int Y, int cx, int cy, uint32_t uFlags);
    void* FindWindowA(const char* lpClassName,const char* lpWindowName);
    uint32_t FindWindowExA(uint32_t hwndParent, uint32_t hwndChildAfter,const char* lpszClass,const char* lpszWindow);
    void* SetParent(void* hWndChild, void* hWndNewParent);
    uint32_t SendMessageTimeoutA(void* hWnd, uint32_t Msg, void* wParam, void* lParam, uint32_t fuFlags, uint32_t uTimeout, void* lpdwResult);
    uint32_t ShowWindow(uint32_t hWnd, uint32_t nCmdShow);
    uint32_t SystemParametersInfoA(uint32_t  uiAction,uint32_t uiParam, void* pvParam,uint32_t fWinIni);
    uint32_t SetLayeredWindowAttributes(void* hwnd, uint32_t crKey, char bAlpha, uint32_t dwFlags);

    int strcmp(const char *str1, const char *str2);
    int printf(const char *fmt, ...);
]]

return ffi.C
