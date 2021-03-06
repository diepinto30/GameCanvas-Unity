﻿/*------------------------------------------------------------*/
/// <summary>GameCanvas</summary>
/// <author>Seibe TAKAHASHI</author>
/// <remarks>
/// (c) 2015-2017 Smart Device Programming.
/// This software is released under the MIT License.
/// http://opensource.org/licenses/mit-license.php
/// </remarks>
/*------------------------------------------------------------*/

namespace GameCanvas
{
    using Function  = System.Action;
    using WebSocket = WebSocketSharp.WebSocket;
    using UObj      = UnityEngine.Object;
    using UGameObj  = UnityEngine.GameObject;
    using URect     = UnityEngine.Rect;
    using UVec2     = UnityEngine.Vector2;
    using UVec3     = UnityEngine.Vector3;
    using UVec4     = UnityEngine.Vector4;
    using UQuat     = UnityEngine.Quaternion;
    using UMtrx     = UnityEngine.Matrix4x4;
    using UColor    = UnityEngine.Color;
    using UBlock    = UnityEngine.MaterialPropertyBlock;
    using UTrans    = UnityEngine.Transform;
    using UCamera   = UnityEngine.Camera;
    using UAudio    = UnityEngine.AudioSource;
    using USprite   = UnityEngine.Sprite;
    using UCamTex   = UnityEngine.WebCamTexture;
    using UDebug    = UnityEngine.Debug;

    using SDrawList = System.Collections.Generic.List<UnityEngine.SpriteRenderer>;
    using LDrawList = System.Collections.Generic.List<UnityEngine.LineRenderer>;
    using BlockList = System.Collections.Generic.List<UnityEngine.MaterialPropertyBlock>;
    using TransList = System.Collections.Generic.List<UnityEngine.Transform>;
    using StringHash= System.Collections.Generic.Dictionary<string, object>;
    using SaveData  = SerializableDictionary<string, string>;
    using GCAssetDB = GameCanvasAssetDB;

    /// <summary>
    /// GameCanvas 本体
    /// </summary>
    public class GameCanvas
    {
        #region Singleton

        private static GameCanvas mInstance;

        public static GameCanvas Instance
        {
            get { return mInstance; }
        }
        
        public static GameCanvas CreateInstance()
        {
            if (mInstance == null)
            {
                mInstance = new GameCanvas();
            }

            return mInstance;
        }

        #endregion

        #region Member Classes

        private class GCInternal : SingletonMonoBehaviour<GCInternal>
        {
            private Function mUpdate;

            protected override void Awake()
            {
                base.Awake();
                name = "GameCanvas";
            }

            protected void Update()
            {
                if (mUpdate != null) mUpdate();
            }

            public void Regist(Function update)
            {
                mUpdate = update;
            }
        }

        #endregion

        #region Const/Static Properties

        private const int       cInitSpriteSize     = 32;
        private const int       cInitLineSize       = 16;
        private const int       cCircleResolution   = 24;

        private static readonly UVec2   cVec2Zero   = UVec2.zero;
        private static readonly UVec2   cVec2One    = UVec2.one;
        private static readonly UVec3   cVec3Zero   = UVec3.zero;
        private static readonly UVec3   cVec3One    = UVec3.one;
        private static readonly UQuat   cQuatZero   = UQuat.identity;
        private static readonly UColor  cColorWhite = UColor.white;
        private static readonly UColor  cColorBlack = UColor.black;

        #endregion

        #region Member Properties
        
        private readonly int        cShaderClip     = 0;
        private readonly int        cShaderMainTex  = 0;
        private readonly UVec2[]    cCirclePoints   = null;

        private Function    mStart                  = null;
        private Function    mCalc                   = null;
        private Function    mDraw                   = null;
        private bool        mIsLoaded               = false;

        private GCAssetDB   mAssetDB                = null;         // アセットデータベース
        private int         mNumImage               = 0;            // 認識済みの画像：数量
        private int         mNumSound               = 0;            // 認識済みの音源：数量
        private UCamTex     mWebCamTexture          = null;         // 映像入力
        private SaveData    mSaveData               = null;         // セーブデータ
        private StringHash  mWebCache               = null;         // ネットワークリソースキャッシュ
        private WebSocket   mWs                     = null;         // WebSocket

        private int         mDeviceWidth            = 0;            // 端末解像度：横幅
        private int         mDeviceHeight           = 0;            // 端末解像度：縦幅
        private int         mCanvasWidthWB          = 0;            // 実効解像度：横幅
        private int         mCanvasHeightWB         = 0;            // 実効解像度：縦幅
        private int         mCanvasWidth            = 0;            // 描画解像度：横幅
        private int         mCanvasHeight           = 0;            // 描画解像度：縦幅
        private float       mCanvasScale            = 1f;           // キャンバス解像度 / デバイス解像度
        private UVec2       mCanvasBorder           = cVec2Zero;    // キャンバス左・上の黒縁の大きさ

        private int         mRendererIndex          = 0;            // 描画：現在の描画インデックス
        private int         mRendererMax            = 0;            // 描画：描画インデックスの暫定最大値（前フレームの2倍）
        private UColor      mRendererColor          = cColorBlack;  // 描画：現在のパレットカラー
        private int         mSpriteIndex            = 0;            // 塗り描画：現在のスプライトインデックス
        private UBlock      mSpriteBlock            = null;         // 塗り描画：マテリアルプロパティーブロック
        private SDrawList   mSprites                = null;         // 塗り描画：スプライトレンダラーキャッシュ
        private TransList   mSpriteTransforms       = null;         // 塗り描画：スプライトトランスフォームキャッシュ
        private int         mLineIndex              = 0;            // 線描画：現在のラインインデックス
        private LDrawList   mLines                  = null;         // 線描画：ラインレンダラーキャッシュ
        private TransList   mLineTransforms         = null;         // 線描画：ライントランスフォームキャッシュ
        private float       mLineWidth              = 2f;           // 線描画：現在の線の太さ
        private float       mFontSize               = 20;           // 文字描画：現在のフォントサイズ
        private float       mTextLineHeight         = 1.65f;        // 文字描画：フォントサイズに対するテキスト1行の高さ倍率
        private float       mTextTracking           = 1.0f;         // 文字描画：トラッキング（字送り）
        private float       mTextHorizontalRatio    = 1.0f;         // 文字描画：水平比率

        private bool        mTouchSupported         = false;        // タッチ：実行環境がタッチ操作対応かどうか
        private bool        mIsTouch                = false;        // タッチ：タッチされているかどうか
        private bool        mIsTouchBegan           = false;        // タッチ：タッチされ始めた瞬間かどうか
        private bool        mIsTouchEnded           = false;        // タッチ：タッチされ終えた瞬間かどうか
        private bool        mIsTapped               = false;        // タッチ：タップされた瞬間かどうか
        private bool        mIsFlicked              = false;        // タッチ：フリックされた瞬間かどうか
        private UVec2       mUnscaledTouchPoint     = -cVec2One;    // タッチ：座標
        private UVec2       mTouchPoint             = -cVec2One;    // タッチ：キャンバスピクセルに対応する座標
        private UVec2       mTouchBeganPoint        = -cVec2One;    // タッチ：開始座標
        private float       mTouchTimeLength        = 0f;           // タッチ：連続時間(秒)
        private float       mTouchHoldTimeLength    = 0f;           // タッチ：連続静止時間(秒)
        private float       mMaxTapTimeLength       = 0.2f;         // タッチ：タップ判定時間長
        private float       mMinFlickDistance       = 1f;           // タッチ：フリック判定移動量
        private float       mMaxTapDistance         = 0.9f;         // タッチ：タップ判定移動量
        private float       mMinHoldTimeLength      = 0.4f;         // タッチ：ホールド判定フレーム数
        private float       mPinchLength            = 0f;           // ピンチインアウト：2点間距離
        private float       mPinchLengthBegan       = 0f;           // ピンチインアウト：2点間距離：タッチ開始時
        private float       mPinchScale             = 0f;           // ピンチインアウト：拡縮率：前フレーム差分
        private float       mPinchScaleBegan        = 0f;           // ピンチインアウト：拡縮率：タッチ開始時から
        private float       mMaxPinchInScale        = 0.95f;        // ピンチインアウト：ピンチイン判定縮小率
        private float       mMinPinchOutScale       = 1.05f;        // ピンチインアウト：ピンチアウト判定拡大率
        private UVec2       mMousePrevPoint         = -cVec2One;    // マウス互換：前回マウス位置
        
        private GCInternal  mGCInternal             = null;         // MonoBehaviour
        private UTrans      mTransform              = null;         // コンポーネント：Transform
        private UCamera     mMainCamera             = null;         // コンポーネント：Camera
        private UAudio      mAudioSE                = null;         // コンポーネント：AudioClip
        private UAudio      mAudioBGM               = null;         // コンポーネント：AudioClip
        
        #endregion

        #region Constructor

        private GameCanvas()
        {
            // 実行環境の記録
            mDeviceWidth = UnityEngine.Screen.width;
            mDeviceHeight = UnityEngine.Screen.height;
            mTouchSupported = UnityEngine.Input.touchSupported;

            // アプリケーションの設定
            UnityEngine.Application.targetFrameRate = 60;
            UnityEngine.Screen.fullScreen = false;
            UnityEngine.Screen.sleepTimeout = UnityEngine.SleepTimeout.NeverSleep;
            UnityEngine.Screen.orientation = UnityEngine.ScreenOrientation.Landscape;
            UnityEngine.Input.multiTouchEnabled = true;
            UnityEngine.Input.simulateMouseWithTouches = true;

            // セーブデータの読み込み
            ReadDataByStorage();

            // ルートオブジェクトの生成
            mGCInternal = GCInternal.Instance;
            mGCInternal.Regist(_Update);
            mTransform = mGCInternal.transform;

            // Cameraコンポーネントの配置。既に配置されているCameraは問答無用で抹消する
            {
                var camArray = UObj.FindObjectsOfType<UCamera>();
                foreach (UCamera cam in camArray)
                {
                    UObj.DestroyImmediate(cam.gameObject);
                }

                var obj = new UGameObj("Camera");
                obj.tag = "MainCamera";
                obj.transform.parent = mTransform;

                mMainCamera = obj.AddComponent<UCamera>();

                mMainCamera.clearFlags = UnityEngine.CameraClearFlags.Depth;
                mMainCamera.orthographic = true;
                var pos = mMainCamera.transform.position;
                pos.x = 0f;
                pos.y = 0f;
                pos.z = 10f;
                mMainCamera.transform.position = pos;
                mMainCamera.transform.localRotation = UQuat.Euler(180f, 0f, 0f);

                obj.AddComponent<UnityEngine.AudioListener>();
            }

            // AudioSourceコンポーネントの配置。サウンドの再生に用いる
            {
                var obj = new UGameObj("Sound");
                obj.transform.parent = mTransform;

                mAudioSE = obj.AddComponent<UAudio>();
                mAudioSE.loop = false;
                mAudioSE.playOnAwake = false;
                mAudioSE.spatialBlend = 0f;

                mAudioBGM = obj.AddComponent<UAudio>();
                mAudioBGM.loop = true;
                mAudioBGM.playOnAwake = false;
                mAudioBGM.spatialBlend = 0f;
            }

            // キャンバスの初期化
            {
                mRendererIndex = 0;
                mRendererColor = cColorWhite;
                mRendererMax = (cInitSpriteSize + cInitLineSize) * 2;

                mSpriteIndex = 0;
                mSpriteBlock = new UBlock();
                mSprites = new SDrawList(cInitSpriteSize);
                mSpriteTransforms = new TransList(cInitSpriteSize);
                cShaderClip = UnityEngine.Shader.PropertyToID("_Clip");
                cShaderMainTex = UnityEngine.Shader.PropertyToID("_MainTex");

                mLineIndex = 0;
                mLines = new LDrawList(cInitLineSize);
                mLineTransforms = new TransList(cInitLineSize);

                SetResolution(640, 480);

                // [Note] レンダラーの初期化はリソース読み込み後に行う
            }

            // 円弧の事前計算
            cCirclePoints = new UVec2[cCircleResolution];
            var step = UnityEngine.Mathf.PI * 2 / cCircleResolution;
            for (var i = 0; i < 24; ++i)
            {
                var rx = UnityEngine.Mathf.Sin(i * step);
                var ry = UnityEngine.Mathf.Cos(i * step);
                cCirclePoints[i] = new UVec2(rx, ry);
            }

            // 外部画像・音源データの読み込み
            mIsLoaded = false;
            mGCInternal.StartCoroutine(_LoadResourceAll());

            // WebCacheの用意
            mWebCache = new StringHash();
        }

        #endregion

        #region Member Methods

        #region UnityGC：イベントAPI

        /// <summary>
        /// イベントを登録します
        /// </summary>
        /// <param name="start"></param>
        /// <param name="calc"></param>
        /// <param name="draw"></param>
        public void Regist(Function start, Function calc, Function draw)
        {
            mStart = start;
            mCalc = calc;
            mDraw = draw;
        }

        private System.Collections.IEnumerator _LoadResourceAll()
        {
            var assetDBReq = UnityEngine.Resources.LoadAsync<GameCanvasAssetDB>("GCAssetDB");

            while (!assetDBReq.isDone)
            {
                yield return null;
            }
            mAssetDB = assetDBReq.asset as GameCanvasAssetDB;
            mNumImage = mAssetDB.images.Length;
            mNumSound = mAssetDB.sounds.Length;

            // レンダラーの初期化
            for (var i = 0; i < cInitSpriteSize; ++i)
            {
                _AddSprite();
            }
            for (var i = 0; i < cInitLineSize; ++i)
            {
                _AddLine();
            }
            mAssetDB.material.SetVector(cShaderClip, UVec4.zero);

            mIsLoaded = true;
            if (mStart != null) mStart();
        }

        private void _Update()
        {
            if(!mIsLoaded) return;

            // 画面サイズ判定
            if (mDeviceWidth != UnityEngine.Screen.width || mDeviceHeight != UnityEngine.Screen.height)
            {
                _UpdateDisplayScale();
            }

            // タッチ情報更新
            _UpdateTouches();

            if (mCalc != null) mCalc();
            if (mDraw != null) mDraw();

            // 描画後処理
            {
                _DrawBorder();

                var numSprite = mSprites.Count;
                for (var i = mSpriteIndex; i < numSprite; ++i)
                {
                    mSprites[i].enabled = false;
                }
                var numLine = mLines.Count;
                for (var i = mLineIndex; i < numLine; ++i)
                {
                    mLines[i].enabled = false;
                }

                mRendererMax = mRendererIndex * 2;
                mRendererIndex = 0;
                mSpriteIndex = 0;
                mLineIndex = 0;
            }
        }

        #endregion

        #region UnityGC：グラフィックAPI

        /// <summary>
        /// ゲームの解像度を設定します
        /// </summary>
        /// <param name="width">X軸方向の解像度（幅）</param>
        /// <param name="height">Y軸方向の解像度（高さ）</param>
        public void SetResolution(int width, int height)
        {
            if (mCanvasWidth == width && mCanvasHeight == height) return;

            mCanvasWidth = width;
            mCanvasHeight = height;

            // 自動回転の再設定。回転固定設定は引き継がれる
            isScreenAutoRotation = isScreenAutoRotation;

            _UpdateDisplayScale();

            if (mIsLoaded) ClearScreen();
        }

        /// <summary>
        /// DrawString や DrawRect などで用いる色を指定します
        /// </summary>
        /// <param name="color">塗りの色</param>
        public void SetColor(UColor color)
        {
            mRendererColor = color;
        }

        /// <summary>
        /// DrawString や DrawRect などで用いる色を指定します
        /// </summary>
        /// <param name="red">赤成分 [0～1]</param>
        /// <param name="green">緑成分 [0～1]</param>
        /// <param name="blue">青成分 [0～1]</param>
        /// <param name="alpha">不透明度 [0～1]</param>
        public void SetColor(float red, float green, float blue, float alpha = 1f)
        {
            mRendererColor.r = red;
            mRendererColor.g = green;
            mRendererColor.b = blue;
            mRendererColor.a = alpha;
        }

        /// <summary>
        /// DrawString や DrawRect などで用いる色を、HSV色空間で指定します
        /// </summary>
        /// <param name="h">hue [0～1]</param>
        /// <param name="s">saturation [0～1]</param>
        /// <param name="v">calue [0～1]</param>
        /// <param name="alpha">不透明度 [0～1]</param>
        public void SetColorHSV(float h, float s, float v, float alpha = 1f)
        {
            var c = UColor.HSVToRGB(h, s, v);
            c.a = alpha;
            SetColor(c);
        }

        /// <summary>
        /// DrawRect や DrawCircle などに用いる線の太さを指定します
        /// </summary>
        /// <param name="lineWidth"></param>
        public void SetLineWidth(float lineWidth)
        {
            if (lineWidth <= 0f)
            {
                // 0以下は許容しない
                throw new System.ArgumentOutOfRangeException("lineWidth", "0以下の数値は設定できません");
            }

            mLineWidth = lineWidth;
        }
        
        /// <summary>
        /// 文字描画におけるフォントサイズを設定します。初期値は`20`です
        /// </summary>
        public void SetFontSize(float fontSize)
        {
            if (fontSize <= 0f)
            {
                // 0以下は許容しない
                throw new System.ArgumentOutOfRangeException("fontSize", "0以下の数値は設定できません");
            }

            mFontSize = fontSize;
        }

        /// <summary>
        /// 文字描画における行の高さを、フォントサイズに対する比率で指定します。`1.0f`を指定した場合、行間は無くなります。初期値は`1.65f`です
        /// </summary>
        /// <param name="lineHeight">行の高さ（フォントサイズに対する比率）</param>
        public void SetTextLineHeight(float lineHeight)
        {
            if (lineHeight <= 0f)
            {
                // 0以下は許容しない
                throw new System.ArgumentOutOfRangeException("lineHeight", "0未満の数値は設定できません");
            }

            mTextLineHeight = lineHeight;
        }

        /// <summary>
        /// 文字描画におけるトラッキング（字送り）を設定します。初期値は`1.0f`です
        /// </summary>
        /// <param name="tracking">トラッキング（字送り）</param>
        public void SetTextTracking(float tracking)
        {
            mTextTracking = tracking;
        }

        /// <summary>
        /// 文字の水平比率を設定します。1より大きくすると幅広に、1より小さくすると縦長になります。初期値は`1.0f`です
        /// </summary>
        /// <param name="ratio">文字の水平比率</param>
        public void SetTextHorizontalRatio(float ratio)
        {
            if (ratio == 0f)
            {
                // 0は許容しない
                throw new System.ArgumentOutOfRangeException("ratio", "0は設定できません");
            }

            mTextHorizontalRatio = ratio;
        }

        /// <summary>
        /// 画面を白で塗りつぶします
        /// </summary>
        public void ClearScreen()
        {
            _DrawSprite(mAssetDB.rect, cColorWhite, 0, 0, mCanvasWidth, mCanvasHeight, -128);
        }

        /// <summary>
        /// 線分を描画します
        /// </summary>
        /// <param name="startX">開始点のX座標</param>
        /// <param name="startY">開始点のY座標</param>
        /// <param name="endX">終了点のX座標</param>
        /// <param name="endY">終了点のY座標</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawLine(float startX, float startY, float endX, float endY, sbyte priority = 0)
        {
            var verts = new UVec3[2]
            {
                cVec3Zero,
                new UVec3(endX - startX, endY - startY, 0)
            };
            _DrawLine(verts, mRendererColor, startX, startY, mLineWidth, priority);
        }

        /// <summary>
        /// 中抜きの円を描画します
        /// </summary>
        /// <param name="x">中心点のX座標</param>
        /// <param name="y">中心点のY座標</param>
        /// <param name="radius">半径</param>
        /// <param name="lineWidth">線の太さ</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawCircle(float x, float y, float radius, sbyte priority = 0)
        {
            if (radius <= 0f)
            {
                // 0以下は許容しない
                throw new System.ArgumentOutOfRangeException("radius", "0以下の数値は設定できません");
            }

            var verts = new UVec3[cCircleResolution+1];
            for (var i = 0; i < cCircleResolution; ++i)
            {
                var rx = cCirclePoints[i].x * radius;
                var ry = cCirclePoints[i].y * radius;
                verts[i] = new UVec3(rx, ry, 0);
            }
            verts[cCircleResolution] = verts[0];
            _DrawLine(verts, mRendererColor, x, y, mLineWidth, priority);
        }

        /// <summary>
        /// 中抜きの長方形を描画します
        /// </summary>
        /// <param name="x">左上のX座標</param>
        /// <param name="y">左上のY座標</param>
        /// <param name="width">横幅</param>
        /// <param name="height">縦幅</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawRect(float x, float y, float width, float height, sbyte priority = 0)
        {
            if (width <= 0f)
            {
                // 0以下は許容しない
                throw new System.ArgumentOutOfRangeException("width", "0以下の数値は設定できません");
            }
            if (height <= 0f)
            {
                // 0以下は許容しない
                throw new System.ArgumentOutOfRangeException("height", "0以下の数値は設定できません");
            }

            DrawRotatedRect(x, y, width, height, 0f, 0f, 0f, priority);
        }

        /// <summary>
        /// 画像を描画します
        /// </summary>
        /// <param name="id">描画する画像のID。img0.png ならば 0 を指定します</param>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        public void DrawImage(int id, float x, float y, sbyte priority = 0)
        {
            if (id < 0 || id >= mNumImage)
            {
                throw new System.ArgumentOutOfRangeException("id", "指定されたIDの画像は存在しません");
            }

            _DrawSprite(mAssetDB.images[id], cColorWhite, x, y, 1, 1, priority, true);
        }
        
        /// <summary>
        /// 文字列を描画します。改行文字は無視されます
        /// </summary>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="str">文字列</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawString(float x, float y, string str, sbyte priority = 0)
        {
            if (string.IsNullOrEmpty(str))
            {
                return;
            }

            var strlen = str.Length;
            var scaleX = mFontSize * mTextHorizontalRatio;
            var tracking = scaleX * mTextTracking ;

            for (var i = 0; i < strlen; ++i)
            {
                int n;
                var c = str[i];

                if (c == '\n' || c == '\r') continue;   // 改行(無視)
                else if (c == ' ' || c == '　') continue;// スペース
                else if (c >= '!' && c <= '~') n = c - '!' + 1; // 基本ラテン文字(半角)
                else if (c >= '！' && c <= '～') n = c - '！' + 1; // 基本ラテン文字(全角)
                else if (c >= '、' && c <= '〕') n = c - '、' + 101; // 日本語記号
                else if (c == '〝') n = 122;             // 〝
                else if (c == '〟') n = 123;             // 〟
                else if (c == '〠') n = 124;             // ドクロ
                else if (c >= 'ぁ' && c <= 'ゞ') n = c - 'ぁ' + 125; // ひらがな
                else if (c == '“') n = 221;             // “
                else if (c == '”') n = 222;             // ”
                else if (c == '‘') n = 223;             // ‘
                else if (c == '’') n = 224;             // ’
                else if (c >= 'ァ' && c <= 'ヿ') n = c - 'ァ' + 225; // カタカナ＋中黒＋長音
                else if (c == '･') n = 315;             // 中黒(半角)
                else if (c >= '①' && c <= '⑳') n = c - '①' + 330; // 囲み数字
                else if (c == '￥') n = 325;             // ￥
                else if (c >= '←' && c <= '↓') n = c - '←' + 326; // 矢印
                else if (c >= 'Ⅰ' && c <= 'Ⅹ') n = c - 'Ⅰ' + 350; // ローマ数字
                else if (c == '∞') n = 360;             // ∞
                else if (c == '≪') n = 361;             // ≪
                else if (c == '≫') n = 362;             // ≫
                else if (c == '√') n = 363;             // √
                else if (c == '♪') n = 364;             // ♪
                else if (c == '♭') n = 365;             // ♭
                else if (c == '♯') n = 366;             // ♯
                else if (c == '♂') n = 367;             // ♂
                else if (c == '♀') n = 368;             // ♀
                else if (c == '℃') n = 369;             // ℃
                else if (c == '☆') n = 370;             // ☆
                else if (c == '★') n = 371;             // ★
                else if (c == '○' || c == '〇') n = 372; // ○
                else if (c == '●') n = 373;             // ●
                else if (c == '◎') n = 374;             // ◎
                else if (c == '◇') n = 375;             // ◇
                else if (c == '◆') n = 376;             // ◆
                else if (c == '□') n = 377;             // □
                else if (c == '■') n = 378;             // ■
                else if (c == '△') n = 379;             // △
                else if (c == '▲') n = 380;             // ▲
                else if (c == '▽') n = 381;             // ▽
                else if (c == '▼') n = 382;             // ▼
                else if (c == '♠' || c == '♤') n = 383; // ♠♤(スペード)
                else if (c == '♣' || c == '♧') n = 384; // ♣♧(クローバー)
                else if (c == '♥' || c == '♡') n = 385; // ♥♡(ハート)
                else if (c == '♦' || c == '♢') n = 386; // ♦♢(ダイヤ)
                else if (c == '※') n = 387;             // ※
                else if (c == '…') n = 388;             // …
                else if (c == '─') n = 389;             // ─
                else if (c == '│') n = 390;             // │
                else if (c == '┌') n = 391;             // ┌
                else if (c == '┐') n = 392;             // ┐
                else if (c == '└') n = 393;             // └
                else if (c == '┘') n = 394;             // ┘
                else if (c == '├') n = 395;             // ├
                else if (c == '┤') n = 396;             // ┤
                else if (c == '┬') n = 397;             // ┬
                else if (c == '┴') n = 398;             // ┴
                else if (c == '┼') n = 399;             // ┼
                else n = 400; // その他(豆腐に置き換え)

                _DrawSprite(mAssetDB.characters[n], mRendererColor, x + i * tracking, y, scaleX, mFontSize, priority, true);
            }
        }

        /// <summary>
        /// 文字列を描画します。改行文字が含まれる場合、複数行で描画します
        /// </summary>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="str">文字列</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawMultiLineString(float x, float y, string str, sbyte priority = 0)
        {
            if (string.IsNullOrEmpty(str))
            {
                return;
            }

            var addY = 0f;
            var lineHeight = mFontSize * mTextLineHeight;

            foreach (var line in str.Split('\n'))
            {
                DrawString(x, y + addY, line, priority);
                addY += lineHeight;
            }
        }

        /// <summary>
        /// 塗りつぶしの円を描画します
        /// </summary>
        /// <param name="x">中心点のX座標</param>
        /// <param name="y">中心点のY座標</param>
        /// <param name="radius">半径</param>
        public void FillCircle(float x, float y, float radius, sbyte priority = 0)
        {
            if (radius <= 0f)
            {
                // 0以下は許容しない
                throw new System.ArgumentOutOfRangeException("radius", "0以下の数値は設定できません");
            }

            _DrawSprite(mAssetDB.circle, mRendererColor, x, y, radius * 2, radius * 2, priority);
        }

        /// <summary>
        /// 塗りつぶしの長方形を描画します
        /// </summary>
        /// <param name="x">左上のX座標</param>
        /// <param name="y">左上のY座標</param>
        /// <param name="width">横幅</param>
        /// <param name="height">縦幅</param>
        public void FillRect(float x, float y, float width, float height, sbyte priority = 0)
        {
            if (width <= 0f)
            {
                // 0以下は許容しない
                throw new System.ArgumentOutOfRangeException("width", "0以下の数値は設定できません");
            }
            if (height <= 0f)
            {
                // 0以下は許容しない
                throw new System.ArgumentOutOfRangeException("height", "0以下の数値は設定できません");
            }

            _DrawSprite(mAssetDB.rect, mRendererColor, x, y, width, height, priority);
        }

        /// <summary>
        /// 中抜きの回転させた長方形を描画します
        /// </summary>
        /// <param name="x">左上のX座標</param>
        /// <param name="y">左上のY座標</param>
        /// <param name="width">横幅</param>
        /// <param name="height">縦幅</param>
        /// <param name="angle">回転角度 (度数法)</param>
        /// <param name="rotationX">長方形の左上を原点としたときの回転の中心位置X</param>
        /// <param name="rotationY">長方形の左上を原点としたときの回転の中心位置Y</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawRotatedRect(float x, float y, float width, float height, float angle, float rotationX = 0f, float rotationY = 0f, sbyte priority = 0)
        {
            if (width <= 0f)
            {
                // 0以下は許容しない
                throw new System.ArgumentOutOfRangeException("width", "0以下の数値は設定できません");
            }
            if (height <= 0f)
            {
                // 0以下は許容しない
                throw new System.ArgumentOutOfRangeException("height", "0以下の数値は設定できません");
            }

            var verts = new UVec3[5]
            {
                cVec3Zero,
                new UVec2(width, 0),
                new UVec2(width, height),
                new UVec2(0, height),
                cVec3Zero
            };
            _DrawLine(verts, mRendererColor, x, y, mLineWidth, angle, rotationX, rotationY, priority);
        }

        /// <summary>
        /// 回転させた画像を描画します
        /// </summary>
        /// <param name="id">描画する画像のID。img0.png ならば 0 を指定します</param>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="angle">回転角度 (度数法)</param>
        /// <param name="rotationX">画像左上を原点としたときの回転の中心位置X</param>
        /// <param name="rotationY">画像左上を原点としたときの回転の中心位置Y</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawRotatedImage(int id, float x, float y, float angle, float rotationX = 0f, float rotationY = 0f, sbyte priority = 0)
        {
            if (id < 0 || id >= mNumImage)
            {
                throw new System.ArgumentOutOfRangeException("id", "指定されたIDの画像は存在しません");
            }

            _DrawSprite(mAssetDB.images[id], cColorWhite, x, y, 1f, 1f, angle, rotationX, rotationY, 0f, 0f, 0f, 0f, priority, true);
        }

        /// <summary>
        /// 塗りつぶしの回転させた長方形を描画します
        /// </summary>
        /// <param name="x">左上のX座標</param>
        /// <param name="y">左上のY座標</param>
        /// <param name="width">横幅</param>
        /// <param name="height">縦幅</param>
        /// <param name="angle">回転角度 (度数法)</param>
        /// <param name="rotationX">長方形の左上を原点としたときの回転の中心位置X</param>
        /// <param name="rotationY">長方形の左上を原点としたときの回転の中心位置Y</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void FillRotatedRect(float x, float y, float width, float height, float angle, float rotationX = 0f, float rotationY = 0f, sbyte priority = 0)
        {
            if (width <= 0f)
            {
                // 0以下は許容しない
                throw new System.ArgumentOutOfRangeException("width", "0以下の数値は設定できません");
            }
            if (height <= 0f)
            {
                // 0以下は許容しない
                throw new System.ArgumentOutOfRangeException("height", "0以下の数値は設定できません");
            }

            _DrawSprite(mAssetDB.rect, mRendererColor, x, y, width, height, angle, rotationX, rotationY, 0f, 0f, 0f, 0f, priority);
        }

        /// <summary>
        /// 一部分を切り取った画像を描画します
        /// </summary>
        /// <param name="id">描画する画像のID。img0.png ならば 0 を指定します</param>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="clipTop">画像上側の切り取る縦幅</param>
        /// <param name="clipRight">画像右側の切り取る横幅</param>
        /// <param name="clipBottom">画像下側の切り取る縦幅</param>
        /// <param name="clipLeft">画像左側の切り取る横幅</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawClippedImage(int id, float x, float y, float clipTop, float clipRight, float clipBottom, float clipLeft, sbyte priority = 0)
        {
            if (id < 0 || id >= mNumImage)
            {
                throw new System.ArgumentOutOfRangeException("id", "指定されたIDの画像は存在しません");
            }
            if (clipTop < 0f)
            {
                throw new System.ArgumentOutOfRangeException("clipTop", "0未満の切り取り幅は指定できません");
            }
            if (clipRight < 0f)
            {
                throw new System.ArgumentOutOfRangeException("clipRight", "0未満の切り取り幅は指定できません");
            }
            if (clipBottom < 0f)
            {
                throw new System.ArgumentOutOfRangeException("clipBottom", "0未満の切り取り幅は指定できません");
            }
            if (clipLeft < 0f)
            {
                throw new System.ArgumentOutOfRangeException("clipLeft", "0未満の切り取り幅は指定できません");
            }

            _DrawSprite(mAssetDB.images[id], cColorWhite, x, y, 1f, 1f, 0f, 0f, 0f, clipTop, clipRight, clipBottom, clipLeft, priority, true);
        }

        /// <summary>
        /// 大きさを変えた画像を描画します
        /// </summary>
        /// <param name="id">描画する画像のID。img0.png ならば 0 を指定します</param>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="scaleH">横の拡縮率</param>
        /// <param name="scaleV">縦の拡縮率</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawScaledImage(int id, float x, float y, float scaleH, float scaleV, sbyte priority = 0)
        {
            if (id < 0 || id >= mNumImage)
            {
                throw new System.ArgumentOutOfRangeException("id", "指定されたIDの画像は存在しません");
            }
            if (scaleH == 0f || scaleV == 0f)
            {
                // 0は許容しない
                return;
            }

            _DrawSprite(mAssetDB.images[id], cColorWhite, x, y, scaleH, scaleV, priority, true);
        }

        /// <summary>
        /// 画像を位置・拡縮率・回転角度を指定して描画します
        /// </summary>
        /// <param name="id">描画する画像のID。img0.png ならば 0 を指定します</param>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="scaleH">縦の拡縮率</param>
        /// <param name="scaleV">横の拡縮率</param>
        /// <param name="angle">回転角度 (度数法)</param>
        /// <param name="rotationX">画像左上を原点としたときの回転の中心位置X</param>
        /// <param name="rotationY">画像左上を原点としたときの回転の中心位置Y</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawImageSRT(int id, float x, float y, float scaleH, float scaleV, float angle, float rotationX = 0f, float rotationY = 0f, sbyte priority = 0)
        {
            if (id < 0 || id >= mNumImage)
            {
                throw new System.ArgumentOutOfRangeException("id", "指定されたIDの画像は存在しません");
            }
            if (scaleH == 0f || scaleV == 0f)
            {
                // 0は許容しない
                return;
            }

            _DrawSprite(mAssetDB.images[id], cColorWhite, x, y, scaleH, scaleV, angle, rotationX, rotationY, 0f, 0f, 0f, 0f, priority, true);
        }

        /// <summary>
        /// 一部分を切り取った画像を、位置・拡縮率・回転角度を指定して描画します
        /// </summary>
        /// <param name="id">描画する画像のID。img0.png ならば 0 を指定します</param>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="clipTop">画像上側の切り取る縦幅</param>
        /// <param name="clipRight">画像右側の切り取る横幅</param>
        /// <param name="clipBottom">画像下側の切り取る縦幅</param>
        /// <param name="clipLeft">画像左側の切り取る横幅</param>
        /// <param name="scaleH">縦の拡縮率</param>
        /// <param name="scaleV">横の拡縮率</param>
        /// <param name="angle">回転角度 (度数法)</param>
        /// <param name="rotationX">画像左上を原点としたときの回転の中心位置X</param>
        /// <param name="rotationY">画像左上を原点としたときの回転の中心位置Y</param>
        [System.Obsolete("DrawClippedImageSRT() は廃止されました", true)]
        public void DrawClippedImageSRT(int id, float x, float y, float clipTop, float clipRight, float clipBottom, float clipLeft, float scaleH, float scaleV, float angle, float rotationX = 0f, float rotationY = 0f)
        {
            //
        }
        
        /// <summary>
        /// 指定された画像の横幅を返します。画像が見つからない場合 0 を返します
        /// </summary>
        /// <param name="id">描画する画像のID。img0.png ならば 0 を指定します</param>
        /// <returns>指定された画像の横幅</returns>
        public int GetImageWidth(int id)
        {
            if (id < 0 || id >= mNumImage)
            {
                throw new System.ArgumentOutOfRangeException("id", "指定されたIDの画像は存在しません");
            }

            return (int)mAssetDB.images[id].rect.width;
        }

        /// <summary>
        /// 指定された画像の高さを返します。画像が見つからない場合 0 を返します
        /// </summary>
        /// <param name="id">描画する画像のID。img0.png ならば 0 を指定します</param>
        /// <returns>指定された画像の高さ</returns>
        public int GetImageHeight(int id)
        {
            if (id < 0 || id >= mNumImage)
            {
                throw new System.ArgumentOutOfRangeException("id", "指定されたIDの画像は存在しません");
            }

            return (int)mAssetDB.images[id].rect.height;
        }

        /// <summary>
        /// FPS（1秒あたりのフレーム更新回数）
        /// </summary>
        public int frameRate
        {
            set
            {
                UnityEngine.Application.targetFrameRate = value;
            }
            get
            {
                return UnityEngine.Application.targetFrameRate;
            }
        }

        /// <summary>
        /// 画面X軸方向のゲーム解像度（幅）
        /// </summary>
        public int screenWidth
        {
            get
            {
                return mCanvasWidth;
            }
        }

        /// <summary>
        /// 画面Y軸方向のゲーム解像度（高さ）
        /// </summary>
        public int screenHeight
        {
            get
            {
                return mCanvasHeight;
            }
        }

        /// <summary>
        /// フルスクリーンかどうか
        /// </summary>
        public bool isFullScreen
        {
            set
            {
                UnityEngine.Screen.fullScreen = value;
                SetResolution(mCanvasWidth, mCanvasHeight);
            }
            get
            {
                return UnityEngine.Screen.fullScreen;
            }
        }

        /// <summary>
        /// ゲーム画面が端末の向きに合わせて自動回転するかどうか
        /// </summary>
        public bool isScreenAutoRotation
        {
            set
            {
                if (value)
                {
                    UnityEngine.Screen.orientation = UnityEngine.ScreenOrientation.AutoRotation;
                }
                else
                {
                    UnityEngine.Screen.orientation = isPortrait ? UnityEngine.ScreenOrientation.Portrait : UnityEngine.ScreenOrientation.Landscape;
                }
            }
            get
            {
                return UnityEngine.Screen.orientation == UnityEngine.ScreenOrientation.AutoRotation;
            }
        }

        /// <summary>
        /// ゲーム画面が縦向きかどうか。この値はゲーム解像度によって自動的に決定されます
        /// </summary>
        public bool isPortrait
        {
            get
            {
                return mCanvasWidth <= mCanvasHeight;
            }
        }

        private void _DrawBorder()
        {
            if (mCanvasBorder.x > 0)
            {
                _DrawSprite(mAssetDB.rect, cColorBlack, -mCanvasBorder.x, 0, mCanvasBorder.x, mCanvasHeight, 127);
                _DrawSprite(mAssetDB.rect, cColorBlack, mCanvasWidth, 0, mCanvasBorder.x, mCanvasHeight, 127);
            }
            else if (mCanvasBorder.y > 0)
            {
                _DrawSprite(mAssetDB.rect, cColorBlack, 0, -mCanvasBorder.y, mCanvasWidth, mCanvasBorder.y, 127);
                _DrawSprite(mAssetDB.rect, cColorBlack, 0, mCanvasHeight, mCanvasWidth, mCanvasBorder.y, 127);
            }
        }

        private void _DrawSprite(USprite sprite, UColor color, float x, float y, float scaleX, float scaleY, sbyte priority, bool flipY = false)
        {
            _DrawSprite(sprite, color, x, y, scaleX, scaleY, 0f, 0f, 0, 0f, 0f, 0f, 0f, priority, flipY);
        }

        private void _DrawSprite(USprite sprite, UColor color, float x, float y, float scaleX, float scaleY, float angle, float rotationX, float rotationY, float clipTop, float clipRight, float clipBottom, float clipLeft, sbyte priority, bool flipY = false)
        {
            var i = mSpriteIndex;

            // オブジェクトが足りなければ補充
            if (i >= mSprites.Count)
            {
                _AddSprite();
            }

            // クリッピング
            mSprites[i].GetPropertyBlock(mSpriteBlock);
            mSpriteBlock.SetVector(cShaderClip, UVec4.zero);
            if (clipLeft != 0f || clipTop != 0f || clipRight != 0f || clipBottom != 0f)
            {
                var w = sprite.rect.width;
                var h = sprite.rect.height;
                if (clipLeft + clipRight > w || clipTop + clipBottom >= h) return;
                var cl = (mCanvasBorder.x + x) / mCanvasScale;
                var ct = (mCanvasBorder.y + y) / mCanvasScale;
                var cr = (mCanvasBorder.x + x + w - clipLeft - clipRight) / mCanvasScale;
                var cb = (mCanvasBorder.y + y + h - clipTop - clipBottom) / mCanvasScale;
                mSpriteBlock.SetVector(cShaderClip, new UVec4(cl, ct, cr, cb));
            }
            mSprites[i].SetPropertyBlock(mSpriteBlock);

            mSprites[i].enabled = true;
            mSprites[i].sharedMaterial = mAssetDB.material;
            mSprites[i].sprite = sprite;
            mSprites[i].color = color;
            mSprites[i].flipY = flipY;

            if (angle == 0f || (rotationX == 0f && rotationY == 0f))
            {
                var pos = mSpriteTransforms[i].position;
                pos.Set(mCanvasBorder.x + x - clipLeft, mCanvasBorder.y + y - clipTop, _CalcTransformZ(priority));
                mSpriteTransforms[i].position = pos;

                var scale = mSpriteTransforms[i].localScale;
                scale.Set(scaleX, scaleY, 1f);
                mSpriteTransforms[i].localScale = scale;

                mSpriteTransforms[i].rotation = angle == 0f
                    ? cQuatZero
                    : UQuat.Euler(0f, 0f, angle);
            }
            else
            {
                var m = UMtrx.TRS(new UVec3(x + rotationX, y + rotationY, 0f), UQuat.Euler(0f, 0f, angle), cVec3One);
                m *= UMtrx.TRS(new UVec3(-rotationX, -rotationY, 0f), cQuatZero, new UVec3(scaleX, scaleY, 1f));

                var pos = mSpriteTransforms[i].position;
                pos.Set(mCanvasBorder.x + m.m03, mCanvasBorder.y + m.m13, _CalcTransformZ(priority));
                mSpriteTransforms[i].position = pos;

                var scale = mSpriteTransforms[i].localScale;
                var sX = UnityEngine.Mathf.Sqrt(m.m00 * m.m00 + m.m01 * m.m01 + m.m02 * m.m02);
                var sY = UnityEngine.Mathf.Sqrt(m.m10 * m.m10 + m.m11 * m.m11 + m.m12 * m.m12);
                scale.Set(sX, sY, 1f);
                mSpriteTransforms[i].localScale = scale;

                mSpriteTransforms[i].rotation = UQuat.LookRotation(m.GetColumn(2), m.GetColumn(1));
            }

            ++mRendererIndex;
            ++mSpriteIndex;
        }

        private void _DrawLine(UVec3[] verts, UColor color, float x, float y, float lineWidth, sbyte priority)
        {
            _DrawLine(verts, color, x, y, lineWidth, 0f, 0f, 0f, priority);
        }

        private void _DrawLine(UVec3[] verts, UColor color, float x, float y, float lineWidth, float angle, float rotationX, float rotationY, sbyte priority)
        {
            var i = mLineIndex;
            
            // オブジェクトが足りなければ補充
            if (i >= mLines.Count)
            {
                _AddLine();
            }

            mLines[i].enabled = true;
#if UNITY_5_4
            mLines[i].SetColors(color, color);
            mLines[i].SetWidth(lineWidth, lineWidth);
            mLines[i].SetVertexCount(verts.Length);
#else
            mLines[i].startColor = color;
            mLines[i].endColor = color;
            mLines[i].startWidth = lineWidth;
            mLines[i].endWidth = lineWidth;
            mLines[i].numPositions = verts.Length;
#endif
            mLines[i].SetPositions(verts);
            if (angle == 0f || (rotationX == 0f && rotationY == 0f))
            {
                var pos = mLineTransforms[i].position;
                pos.x = mCanvasBorder.x + x;
                pos.y = mCanvasBorder.y + y;
                pos.z = _CalcTransformZ(priority);
                mLineTransforms[i].position = pos;

                mLineTransforms[i].rotation = angle == 0f
                    ? cQuatZero
                    : UQuat.Euler(0f, 0f, angle);
            }
            else
            {
                var m = UMtrx.TRS(new UVec3(x + rotationX, y + rotationY, 0f), UQuat.Euler(0f, 0f, angle), cVec3One);
                m *= UMtrx.TRS(new UVec3(-rotationX, -rotationY, 0f), cQuatZero, cVec3One);

                var pos = mLineTransforms[i].position;
                pos.x = mCanvasBorder.x + m.m03;
                pos.y = mCanvasBorder.y + m.m13;
                pos.z = _CalcTransformZ(priority);
                mLineTransforms[i].position = pos;

                mLineTransforms[i].rotation = UQuat.LookRotation(m.GetColumn(2), m.GetColumn(1));
            }

            ++mRendererIndex;
            ++mLineIndex;
        }

        private float _CalcTransformZ(sbyte priority)
        {
            return (priority * 2 - 256) + ((float)mRendererIndex / mRendererMax * 2);
        }

        private void _AddSprite()
        {
            var obj = new UGameObj("SpriteRenderer");
            obj.transform.parent = mTransform;
            var renderer = obj.AddComponent<UnityEngine.SpriteRenderer>();
            renderer.enabled = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.receiveShadows = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.sharedMaterial = mAssetDB.material;
            renderer.sprite = null;
            renderer.color = cColorWhite;
            mSprites.Add(renderer);
            mSpriteTransforms.Add(obj.transform);
        }

        private void _AddLine()
        {
            var obj = new UGameObj("LineRenderer");
            obj.transform.parent = mTransform;
            var renderer = obj.AddComponent<UnityEngine.LineRenderer>();
            renderer.enabled = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            renderer.receiveShadows = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.sharedMaterial = mAssetDB.material;
#if UNITY_5_4
            renderer.motionVectors = false;
            renderer.SetColors(cColorWhite, cColorWhite);
#else
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.startColor = cColorWhite;
            renderer.endColor = cColorWhite;
#endif
            renderer.useWorldSpace = false;
            mLines.Add(renderer);
            mLineTransforms.Add(obj.transform);
        }

        private void _UpdateDisplayScale()
        {
            // 表示倍率の計算
            mDeviceWidth = UnityEngine.Screen.width;
            mDeviceHeight = UnityEngine.Screen.height;
            var scaleWidth = (float)mCanvasWidth / mDeviceWidth;
            var scaleHeight = (float)mCanvasHeight / mDeviceHeight;
            mCanvasScale = UnityEngine.Mathf.Max(scaleWidth, scaleHeight);

            // 実効解像度の変更
            mCanvasWidthWB = UnityEngine.Mathf.FloorToInt(UnityEngine.Screen.width * mCanvasScale);
            mCanvasHeightWB = UnityEngine.Mathf.FloorToInt(UnityEngine.Screen.height * mCanvasScale);
            //UnityEngine.Screen.SetResolution(_canvasWidthWB, _canvasHeightWB, isFullScreen);

            // 黒縁の計算
            mCanvasBorder.x = (mCanvasWidthWB - mCanvasWidth) * 0.5f;
            mCanvasBorder.y = (mCanvasHeightWB - mCanvasHeight) * 0.5f;

            // カメラ倍率
            mMainCamera.orthographicSize = mCanvasHeightWB * 0.5f;
            var cameraPos = mMainCamera.transform.position;
            cameraPos.x = mCanvasBorder.x + mCanvasWidth / 2;
            cameraPos.y = mCanvasBorder.y + mCanvasHeight / 2;
            mMainCamera.transform.position = cameraPos;
        }

        #endregion

        #region UnityGC：グラフィックAPI (カメラ映像)

        /// <summary>
        /// カメラ映像の画素数（横幅）
        /// </summary>
        public int cameraImageWidth
        {
            get { return mWebCamTexture == null ? 0 : mWebCamTexture.width; }
        }

        /// <summary>
        /// カメラ映像の画素数（高さ）
        /// </summary>
        public int cameraImageHeight
        {
            get { return mWebCamTexture == null ? 0 : mWebCamTexture.height; }
        }

        /// <summary>
        /// カメラ映像入力を有効にします
        /// </summary>
        /// <param name="isFrontFacing">前面カメラを優先するかどうか</param>
        /// <param name="requestedWidth">カメラ映像の要求画素数（幅）</param>
        /// <param name="requestedHeight">カメラ映像の要求画素数（高さ）</param>
        /// <param name="requestedFPS">カメラ映像の要求フレームレート</param>
        public void StartCameraService(bool isFrontFacing = false, int requestedWidth = 320, int requestedHeight = 240, int requestedFPS = 30)
        {
            var devices = UCamTex.devices;
            if (devices.Length == 0)
            {
                UDebug.LogWarning("カメラ映像入力を検出できません");
                return;
            }

            var targetDeviceName = devices[0].name;
            foreach (var device in devices)
            {
                if (device.isFrontFacing == isFrontFacing)
                {
                    targetDeviceName = device.name;
                    break;
                }
            }

            mWebCamTexture = new UCamTex(targetDeviceName, requestedWidth, requestedHeight, requestedFPS);
            if (mWebCamTexture == null)
            {
                UDebug.LogWarning("要求されたカメラ映像が見つかりません");
                return;
            }

            mWebCamTexture.Play();
        }

        /// <summary>
        /// カメラ映像入力を無効にします
        /// </summary>
        public void StopCameraService()
        {
            mWebCamTexture.Stop();
            mWebCamTexture = null;
        }

        /// <summary>
        /// カメラ映像を描画します
        /// </summary>
        /// <param name="id">描画する画像のID。img0.png ならば 0 を指定します</param>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawCameraImage(float x, float y, sbyte priority = 0)
        {
            if (mWebCamTexture == null) return;

            _DrawWebCamTexture(mWebCamTexture, cColorWhite, x, y, 1f, 1f, priority);
        }

        /// <summary>
        /// カメラ映像を一部分を切り取って描画します
        /// </summary>
        /// <param name="id">描画する画像のID。img0.png ならば 0 を指定します</param>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="clipTop">画像上側の切り取る縦幅</param>
        /// <param name="clipRight">画像右側の切り取る横幅</param>
        /// <param name="clipBottom">画像下側の切り取る縦幅</param>
        /// <param name="clipLeft">画像左側の切り取る横幅</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawClippedCameraImage(float x, float y, float clipTop, float clipRight, float clipBottom, float clipLeft, sbyte priority = 0)
        {
            if (mWebCamTexture == null) return;
            if (clipTop < 0f)
            {
                throw new System.ArgumentOutOfRangeException("clipTop", "0未満の切り取り幅は指定できません");
            }
            if (clipRight < 0f)
            {
                throw new System.ArgumentOutOfRangeException("clipRight", "0未満の切り取り幅は指定できません");
            }
            if (clipBottom < 0f)
            {
                throw new System.ArgumentOutOfRangeException("clipBottom", "0未満の切り取り幅は指定できません");
            }
            if (clipLeft < 0f)
            {
                throw new System.ArgumentOutOfRangeException("clipLeft", "0未満の切り取り幅は指定できません");
            }

            _DrawWebCamTexture(mWebCamTexture, cColorWhite, x, y, 1f, 1f, 0f, 0f, 0f, clipTop, clipRight, clipBottom, clipLeft, priority);
        }

        /// <summary>
        /// カメラ映像を大きさを変えて描画します
        /// </summary>
        /// <param name="id">描画する画像のID。img0.png ならば 0 を指定します</param>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="scaleH">横の拡縮率</param>
        /// <param name="scaleV">縦の拡縮率</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawScaledCameraImage(float x, float y, float scaleH, float scaleV, sbyte priority = 0)
        {
            if (mWebCamTexture == null || scaleH == 0f || scaleV == 0f) return;

            _DrawWebCamTexture(mWebCamTexture, cColorWhite, x, y, scaleH, scaleV, priority);
        }

        /// <summary>
        /// カメラ映像を回転させて描画します
        /// </summary>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="angle">回転角度 (度数法)</param>
        /// <param name="rotationX">画像左上を原点としたときの回転の中心位置X</param>
        /// <param name="rotationY">画像左上を原点としたときの回転の中心位置Y</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawRotatedCameraImage(float x, float y, float angle, float rotationX = 0f, float rotationY = 0f, sbyte priority = 0)
        {
            if (mWebCamTexture == null) return;

            _DrawWebCamTexture(mWebCamTexture, cColorWhite, x, y, 1f, 1f, angle, rotationX, rotationY, 0f, 0f, 0f, 0f, priority);
        }

        /// <summary>
        /// カメラ映像を位置・拡縮率・回転角度を指定して描画します
        /// </summary>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="scaleH">縦の拡縮率</param>
        /// <param name="scaleV">横の拡縮率</param>
        /// <param name="angle">回転角度 (度数法)</param>
        /// <param name="rotationX">画像左上を原点としたときの回転の中心位置X</param>
        /// <param name="rotationY">画像左上を原点としたときの回転の中心位置Y</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawCameraImageSRT(float x, float y, float scaleH, float scaleV, float angle, float rotationX = 0f, float rotationY = 0f, sbyte priority = 0)
        {
            if (mWebCamTexture == null || scaleH == 0f || scaleV == 0f) return;

            _DrawWebCamTexture(mWebCamTexture, cColorWhite, x, y, scaleH, scaleV, angle, rotationX, rotationY, 0f, 0f, 0f, 0f, priority);
        }

        /// <summary>
        /// カメラ映像の一部分を切り取り、位置・拡縮率・回転角度を指定して描画します
        /// </summary>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="clipTop">画像上側の切り取る縦幅</param>
        /// <param name="clipRight">画像右側の切り取る横幅</param>
        /// <param name="clipBottom">画像下側の切り取る縦幅</param>
        /// <param name="clipLeft">画像左側の切り取る横幅</param>
        /// <param name="scaleH">縦の拡縮率</param>
        /// <param name="scaleV">横の拡縮率</param>
        /// <param name="angle">回転角度 (度数法)</param>
        /// <param name="rotationX">画像左上を原点としたときの回転の中心位置X</param>
        /// <param name="rotationY">画像左上を原点としたときの回転の中心位置Y</param>
        [System.Obsolete("DrawClippedCameraImageSRT() は廃止されました", true)]
        public void DrawClippedCameraImageSRT(float x, float y, float clipTop, float clipRight, float clipBottom, float clipLeft, float scaleH, float scaleV, float angle, float rotationX = 0f, float rotationY = 0f)
        {
            /*
            if (_cameraTexture == null)
            {
                Debug.LogWarning("カメラ映像入力が無効です");
                return;
            }

            if (clipLeft < 0 || clipTop < 0 || clipRight < 0 || clipBottom < 0)
            {
                // 負の切り取り幅は許容しない
                Debug.LogWarning("引数の値が不正です");
                return;
            }

            if (scaleH == 0 || scaleV == 0)
            {
                // ゼロの拡縮率は許容しない
                Debug.LogWarning("引数の値が不正です");
                return;
            }

            if (x >= _canvasWidth || y >= _canvasHeight)
            {
                // 描画範囲外である
                return;
            }

            Matrix4x4 mat;
            if (rotationX == 0 && rotationY == 0)
            {
                mat = Matrix4x4.TRS(new Vector3(x, y, 0f), Quaternion.AngleAxis(angle, Vector3.forward), new Vector3(scaleH, scaleV, 1f));
            }
            else
            {
                mat = Matrix4x4.TRS(new Vector3(x + rotationX, y + rotationY, 0f), Quaternion.AngleAxis(angle, Vector3.forward), Vector3.one);
                mat *= Matrix4x4.TRS(new Vector3(-rotationX, -rotationY, 0f), Quaternion.identity, new Vector3(scaleH, scaleV, 1f));
            }

            var clip = new Vector4(clipLeft, clipTop, clipRight, clipBottom);
            _drawQueue.Enqueue(new DrawInfo(_cameraTexture, null, mat.inverse, clip));
            */
        }

        private void _DrawWebCamTexture(UCamTex webcam, UColor color, float x, float y, float scaleX, float scaleY, sbyte priority)
        {
            _DrawWebCamTexture(webcam, color, x, y, scaleX, scaleY, 0f, 0f, 0f, 0f, 0f, 0f, 0f, priority);
        }

        private void _DrawWebCamTexture(UCamTex webcam, UColor color, float x, float y, float scaleX, float scaleY, float angle, float rotationX, float rotationY, float clipTop, float clipRight, float clipBottom, float clipLeft, sbyte priority)
        {
            var i = mSpriteIndex;
            var w = webcam.width;
            var h = webcam.height;

            // オブジェクトが足りなければ補充
            if (i >= mSprites.Count)
            {
                _AddSprite();
            }

            // クリッピング
            mSprites[i].GetPropertyBlock(mSpriteBlock);
            mSpriteBlock.SetVector(cShaderClip, UVec4.zero);
            if (clipLeft != 0f || clipTop != 0f || clipRight != 0f || clipBottom != 0f)
            {
                if (clipLeft + clipRight > w || clipTop + clipBottom >= h) return;
                var cl = (mCanvasBorder.x + x) * mCanvasScale;
                var ct = (mCanvasBorder.y + y) * mCanvasScale;
                var cr = (mCanvasBorder.x + x + w - clipLeft - clipRight) * mCanvasScale;
                var cb = (mCanvasBorder.y + y + h - clipTop - clipBottom) * mCanvasScale;
                mSpriteBlock.SetVector(cShaderClip, new UVec4(cl, ct, cr, cb));
            }
            mSprites[i].SetPropertyBlock(mSpriteBlock);
            
            mSprites[i].enabled = true;
            mSprites[i].sprite = mAssetDB.dummy;
            mSprites[i].color = color;
            mSprites[i].flipY = true;
            mSprites[i].GetPropertyBlock(mSpriteBlock);
            mSpriteBlock.SetTexture(cShaderMainTex, webcam);
            mSprites[i].SetPropertyBlock(mSpriteBlock);

            if (angle == 0f || (rotationX == 0f && rotationY == 0f))
            {
                var pos = mSpriteTransforms[i].position;
                pos.Set(mCanvasBorder.x + x - clipLeft, mCanvasBorder.y + y - clipTop, _CalcTransformZ(priority));
                mSpriteTransforms[i].position = pos;

                var scale = mSpriteTransforms[i].localScale;
                scale.Set(w * scaleX, h * scaleY, 1f);
                mSpriteTransforms[i].localScale = scale;

                mSpriteTransforms[i].rotation = angle == 0f
                    ? cQuatZero
                    : UQuat.Euler(0f, 0f, angle);
            }
            else
            {
                var m = UMtrx.TRS(new UVec3(x + rotationX * scaleX, y + rotationY * scaleY, 0f), UQuat.Euler(0f, 0f, angle), cVec3One);
                m *= UMtrx.TRS(new UVec3(-rotationX * scaleX, -rotationY * scaleY, 0f), cQuatZero, new UVec3(scaleX, scaleY, 1f));

                var pos = mSpriteTransforms[i].position;
                pos.Set(mCanvasBorder.x + m.m03, mCanvasBorder.y + m.m13, _CalcTransformZ(priority));
                mSpriteTransforms[i].position = pos;

                var scale = mSpriteTransforms[i].localScale;
                var sX = w * UnityEngine.Mathf.Sqrt(m.m00 * m.m00 + m.m01 * m.m01 + m.m02 * m.m02);
                var sY = h * UnityEngine.Mathf.Sqrt(m.m10 * m.m10 + m.m11 * m.m11 + m.m12 * m.m12);
                scale.Set(sX, sY, 1f);
                mSpriteTransforms[i].localScale = scale;

                mSpriteTransforms[i].rotation = UQuat.LookRotation(m.GetColumn(2), m.GetColumn(1));
            }

            ++mRendererIndex;
            ++mSpriteIndex;
        }

        #endregion
        
        #region UnityGC：サウンドAPI

        /// <summary>
        /// BGMを再生します。すでに再生しているBGMは停止します
        /// </summary>
        /// <param name="id">再生する音声のID。snd0.png ならば 0 を指定します</param>
        /// <param name="isLoop">ループするかどうか。真の場合、StopBGM()を呼ぶまでループ再生します</param>
        public void PlayBGM(int id, bool isLoop = true)
        {
            if (id < 0 || id >= mNumSound)
            {
                UDebug.LogWarning("存在しないファイルが指定されました");
                return;
            }

            if (mAudioBGM.isPlaying)
            {
                mAudioBGM.Stop();
            }

            mAudioBGM.clip = mAssetDB.sounds[id];
            mAudioBGM.loop = isLoop;
            mAudioBGM.Play();
        }

        /// <summary>
        /// BGMの再生を一時停止します。PlayBGM()で同じ音声を指定することで途中から再生できます
        /// </summary>
        public void PauseBGM()
        {
            if (mAudioBGM.isPlaying)
            {
                mAudioBGM.Pause();
            }
        }

        /// <summary>
        /// BGMの再生を終了します
        /// </summary>
        public void StopBGM()
        {
            if (mAudioBGM.isPlaying)
            {
                mAudioBGM.Stop();
            }
        }

        /// <summary>
        /// BGMの音量を変更します
        /// </summary>
        /// <param name="volume">音量 (0～1)</param>
        public void ChangeBGMVolume(float volume)
        {
            mAudioBGM.volume = UnityEngine.Mathf.Clamp01(volume);
        }

        /// <summary>
        /// SEを再生します。すでに再生しているSEは停止しません
        /// </summary>
        /// <param name="id">再生する音声のID。snd0.png ならば 0 を指定します</param>
        public void PlaySE(int id)
        {
            if (id < 0 || id >= mNumSound)
            {
                UDebug.LogWarning("存在しないファイルが指定されました");
                return;
            }

            mAudioSE.PlayOneShot(mAssetDB.sounds[id]);
        }

        /// <summary>
        /// SEの音量を変更します
        /// </summary>
        /// <param name="volume">音量 (0～1)</param>
        public void ChangeSEVolume(float volume)
        {
            mAudioSE.volume = UnityEngine.Mathf.Clamp01(volume);
        }

        #endregion

        #region UnityGC：入力API (タッチ)

        /// <summary>
        /// タッチされた状態かどうか
        /// </summary>
        public bool isTouch
        {
            get
            {
                return mIsTouch;
            }
        }

        /// <summary>
        /// タッチを始めた瞬間かどうか
        /// </summary>
        public bool isTouchBegan
        {
            get
            {
                return mIsTouchBegan;
            }
        }

        /// <summary>
        /// タッチを終えた瞬間かどうか
        /// </summary>
        public bool isTouchEnded
        {
            get
            {
                return mIsTouchEnded;
            }
        }

        /// <summary>
        /// ホールド（指で触れたまま静止）された状態かどうか
        /// </summary>
        public bool isHold
        {
            get
            {
                return mTouchHoldTimeLength >= mMinHoldTimeLength;
            }
        }

        /// <summary>
        /// タップ（指で軽く触れる）された瞬間かどうか
        /// </summary>
        public bool isTap
        {
            get
            {
                return mIsTapped;
            }
        }

        /// <summary>
        /// フリック（指で軽くはじく）された瞬間かどうか
        /// </summary>
        public bool isFlick
        {
            get
            {
                return mIsFlicked;
            }
        }

        /// <summary>
        /// ピンチインまたはピンチアウトされた状態かどうか
        /// </summary>
        public bool isPinchInOut
        {
            get
            {
                return isPinchIn || isPinchOut;
            }
        }

        /// <summary>
        /// ピンチインされた状態かどうか
        /// </summary>
        public bool isPinchIn
        {
            get
            {
                return mPinchScale != 0f && mPinchScaleBegan < 0.95f;
            }
        }

        /// <summary>
        /// ピンチアウトされた状態かどうか
        /// </summary>
        public bool isPinchOut
        {
            get
            {
                return mPinchScale != 0f && mPinchScaleBegan > 1.05f;
            }
        }

        /// <summary>
        /// タッチされている座標X。タッチされていないときは、最後にタッチされた座標を返します
        /// </summary>
        public int touchX
        {
            get
            {
                return (int)touchPoint.x;
            }
        }

        /// <summary>
        /// タッチされている座標Y。タッチされていないときは、最後にタッチされた座標を返します
        /// </summary>
        public int touchY
        {
            get
            {
                return (int)touchPoint.y;
            }
        }

        /// <summary>
        /// タッチされている座標。タッチされていないときは、最後にタッチされた座標を返します
        /// </summary>
        public UVec2 touchPoint
        {
            get
            {
                return mTouchPoint;
            }
        }

        /// <summary>
        /// 同時にタッチされている数
        /// </summary>
        public int touchCount
        {
            get
            {
                if (mTouchSupported)
                    return UnityEngine.Input.touchCount;
                else
                    return mIsTouch ? 1 : 0;
            }
        }

        /// <summary>
        /// タッチされている時間
        /// </summary>
        public float touchTimeLength
        {
            get
            {
                return mTouchTimeLength;
            }
        }

        /// <summary>
        /// ピンチインアウトの拡縮率。ピンチインアウトされていない場合、0を返します
        /// </summary>
        public float pinchRatio
        {
            get
            {
                return mPinchScaleBegan;
            }
        }

        /// <summary>
        /// 前回フレームを基準としたピンチインアウトの拡縮率。ピンチインアウトされていない場合、0を返します
        /// </summary>
        public float pinchRatioInstant
        {
            get
            {
                return mPinchScale;
            }
        }

        /// <summary>
        /// タップと判定する最長連続タッチ時間（秒）。0 より大きい値である必要があります
        /// </summary>
        public float maxTapTimeLength
        {
            set { if (value > 0f) mMaxTapTimeLength = value; }
            get { return mMaxTapTimeLength; }
        }

        /// <summary>
        /// フリックと判定する最短移動距離。0 より大きい値である必要があります
        /// </summary>
        public float minFlickDistance
        {
            set { if (value > 0f) mMinFlickDistance = value; }
            get { return mMinFlickDistance; }
        }

        /// <summary>
        /// タップと判定する最長移動距離。0 より大きい値である必要があります
        /// </summary>
        public float maxTapDistance
        {
            set { if (value > 0f) mMaxTapDistance = value; }
            get { return mMaxTapDistance; }
        }

        /// <summary>
        /// ホールドと判定する最短タッチ時間（秒）。0 より大きい値である必要があります
        /// </summary>
        public float minHoldTimeLength
        {
            set { if (value > 0f) mMinHoldTimeLength = value; }
            get { return mMinHoldTimeLength; }
        }

        /// <summary>
        /// ピンチインと判定する最大縮小率。0 より大きく 1 より小さい値である必要があります
        /// </summary>
        public float maxPinchInScale
        {
            set { if (value > 0f && value < 1f) mMaxPinchInScale = value; }
            get { return mMaxPinchInScale; }
        }

        /// <summary>
        /// ピンチアウトと判定する最小拡大率。1 より大きい値である必要があります
        /// </summary>
        public float minPinchOutScale
        {
            set { if (value > 1f) mMinPinchOutScale = value; }
            get { return mMinPinchOutScale; }
        }

        /// <summary>
        /// タッチの詳細情報。タッチされていないときは(-1, -1)を返します
        /// </summary>
        /// <param name="fingerId">fingerId</param>
        public UVec2 GetTouchPoint(int fingerId)
        {
            return UnityEngine.Input.touchCount > fingerId ? UnityEngine.Input.GetTouch(fingerId).position : -UVec2.one;
        }
        
        private void _UpdateTouches()
        {
            // 初期化
            mIsTouchBegan = false;
            mIsTouchEnded = false;
            mIsTapped = false;
            mIsFlicked = false;

            // タッチ判定
            mIsTouch = mTouchSupported ? UnityEngine.Input.touchCount > 0 : UnityEngine.Input.GetMouseButton(0) || UnityEngine.Input.GetMouseButtonUp(0);
            if (mIsTouch)
            {
                // 連続時間を記録
                mTouchTimeLength += UnityEngine.Time.deltaTime;

                // タッチ・マウス互換処理
                UnityEngine.TouchPhase phase;
                if (mTouchSupported)
                {
                    var t0 = UnityEngine.Input.GetTouch(0);
                    mUnscaledTouchPoint = t0.position;
                    mUnscaledTouchPoint.y = UnityEngine.Screen.height - mUnscaledTouchPoint.y;
                    phase = t0.phase;
                }
                else
                {
                    mUnscaledTouchPoint = UnityEngine.Input.mousePosition;
                    mUnscaledTouchPoint.y = UnityEngine.Screen.height - mUnscaledTouchPoint.y;
                    if (UnityEngine.Input.GetMouseButtonDown(0)) { phase = UnityEngine.TouchPhase.Began; mMousePrevPoint = mUnscaledTouchPoint; }
                    else if (UnityEngine.Input.GetMouseButtonUp(0)) { phase = UnityEngine.TouchPhase.Ended; mMousePrevPoint = -UVec2.one; }
                    else if (mMousePrevPoint != mUnscaledTouchPoint) { phase = UnityEngine.TouchPhase.Moved; mMousePrevPoint = mUnscaledTouchPoint; }
                    else { phase = UnityEngine.TouchPhase.Stationary; }
                }

                // タッチ座標の変換
                mTouchPoint = mUnscaledTouchPoint * mCanvasScale - mCanvasBorder;

                // タッチ関連挙動の検出
                switch (phase)
                {
                    case UnityEngine.TouchPhase.Began:
                        // 開始時間を記録
                        mTouchBeganPoint = mUnscaledTouchPoint;
                        mIsTouchBegan = true;
                        break;

                    case UnityEngine.TouchPhase.Canceled:
                    case UnityEngine.TouchPhase.Ended:
                        // タップ・フリック判定
                        if (mTouchHoldTimeLength <= mMaxTapTimeLength)
                        {
                            var diff = UVec2.Distance(mTouchBeganPoint, mUnscaledTouchPoint) / UnityEngine.Screen.dpi;
                            mIsFlicked = diff >= mMinFlickDistance;
                            mIsTapped = diff <= mMaxTapDistance;
                        }

                        // 初期化
                        mTouchBeganPoint = -UVec2.one;
                        mTouchTimeLength = 0f;
                        mTouchHoldTimeLength = 0f;
                        mIsTouchEnded = true;
                        break;

                    case UnityEngine.TouchPhase.Moved:
                        // 初期化
                        mTouchHoldTimeLength = 0;
                        break;

                    case UnityEngine.TouchPhase.Stationary:
                        // 連続静止時間を記録
                        mTouchHoldTimeLength += UnityEngine.Time.deltaTime;
                        break;
                }

                // ピンチイン・アウト判定
                if (UnityEngine.Input.touchCount > 1)
                {
                    var t0 = UnityEngine.Input.GetTouch(0);
                    var t1 = UnityEngine.Input.GetTouch(1);

                    switch (t1.phase)
                    {
                        case UnityEngine.TouchPhase.Began:
                            // 2点間の距離を記録
                            mPinchLength = UVec2.Distance(t0.position, t1.position);
                            mPinchLengthBegan = mPinchLength;
                            mPinchScale = 1f;
                            mPinchScaleBegan = 1f;
                            break;

                        case UnityEngine.TouchPhase.Canceled:
                        case UnityEngine.TouchPhase.Ended:
                            // 初期化
                            mPinchLength = 0f;
                            mPinchLengthBegan = 0f;
                            mPinchScale = 0f;
                            mPinchScaleBegan = 0f;
                            break;

                        case UnityEngine.TouchPhase.Moved:
                        case UnityEngine.TouchPhase.Stationary:
                            // ピンチインアウト処理
                            if (t0.phase == UnityEngine.TouchPhase.Moved || t1.phase == UnityEngine.TouchPhase.Moved)
                            {
                                var length = UVec2.Distance(t0.position, t1.position);
                                mPinchScale = length / mPinchLength;
                                mPinchScaleBegan = length / mPinchLengthBegan;
                                mPinchLength = length;
                            }
                            break;
                    }
                }
            }
        }

        #endregion

        #region UnityGC：入力API (加速度)

        /// <summary>
        /// 加速度センサーで測定されたX軸の加速度
        /// </summary>
        public float acceX
        {
            get { return UnityEngine.Input.acceleration.x; }
        }

        /// <summary>
        /// 加速度センサーで測定されたY軸の加速度
        /// </summary>
        public float acceY
        {
            get { return -UnityEngine.Input.acceleration.y; }
        }

        /// <summary>
        /// 加速度センサーで測定されたZ軸の加速度
        /// </summary>
        public float acceZ
        {
            get { return -UnityEngine.Input.acceleration.z; }
        }

        #endregion

        #region UnityGC：入力API (ジャイロスコープ)

        /// <summary>
        /// ジャイロスコープが有効かどうか
        /// </summary>
        public bool isGyroEnabled
        {
            set { UnityEngine.Input.gyro.enabled = value; }
            get { return UnityEngine.Input.gyro.enabled; }
        }

        /// <summary>
        /// ジャイロスコープで測定されたX軸の回転率
        /// </summary>
        public float gyroX
        {
            get { return UnityEngine.Input.gyro.rotationRateUnbiased.x; }
        }

        /// <summary>
        /// ジャイロスコープで測定されたY軸の回転率
        /// </summary>
        public float gyroY
        {
            get { return -UnityEngine.Input.gyro.rotationRateUnbiased.y; }
        }

        /// <summary>
        /// ジャイロスコープで測定されたZ軸の回転率
        /// </summary>
        public float gyroZ
        {
            get { return -UnityEngine.Input.gyro.rotationRateUnbiased.z; }
        }

        #endregion

        #region UnityGC：入力API (コンパス)

        /// <summary>
        /// 地磁気センサーが有効かどうか
        /// </summary>
        public bool isCompassEnabled
        {
            set { UnityEngine.Input.compass.enabled = value; }
            get { return UnityEngine.Input.compass.enabled; }
        }

        /// <summary>
        /// 地磁気センサーで測定された磁北極方向への回転角度 (度数法)
        /// </summary>
        public float compass
        {
            get { return -UnityEngine.Input.compass.magneticHeading; }
        }

        #endregion

        #region UnityGC：入力API (位置情報)

        /// <summary>
        /// 位置情報の取得が有効かどうか。有効でない場合、ユーザーに許可を求めるダイアログが表示される場合があります
        /// </summary>
        public bool isLocationEnabled
        {
            get { return UnityEngine.Input.location.isEnabledByUser; }
        }

        /// <summary>
        /// 位置情報の取得が正常に行われているかどうか
        /// </summary>
        public bool isRunningLocaltionService
        {
            get { return UnityEngine.Input.location.status == UnityEngine.LocationServiceStatus.Running; }
        }

        /// <summary>
        /// 最後に測定した場所の緯度情報
        /// </summary>
        public float lastLocationLatitude
        {
            get { return UnityEngine.Input.location.lastData.latitude; }
        }

        /// <summary>
        /// 最後に測定した場所の経度情報
        /// </summary>
        public float lastLocationLongitude
        {
            get { return UnityEngine.Input.location.lastData.longitude; }
        }

        /// <summary>
        /// 最後に位置情報を取得した時間から現在までの経過秒数
        /// </summary>
        public float lastLocationTime
        {
            get { return (float)(System.DateTime.Now.Subtract(new System.DateTime(1970, 1, 1)).TotalSeconds - UnityEngine.Input.location.lastData.timestamp); }
        }

        /// <summary>
        /// 位置情報の測定を開始します。この操作は一般に多くの電力を消費します
        /// </summary>
        public void StartLocationService()
        {
            UnityEngine.Input.location.Start(5f, 5f);
        }

        /// <summary>
        /// 位置情報の測定を終了します
        /// </summary>
        public void StopLocationService()
        {
            UnityEngine.Input.location.Stop();
        }

        #endregion

        #region UnityGC：入力API (その他)

        /// <summary>
        /// 戻るボタンが押されたかどうか (Androidのみ)
        /// </summary>
        public bool isBackKeyPushed
        {
            get { return UnityEngine.Application.platform == UnityEngine.RuntimePlatform.Android && UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Escape); }
        }

        #endregion

        #region UnityGC：ネットワークAPI (HTTP Download)

        /// <summary>
        /// ネットワークキャッシュをクリアします
        /// </summary>
        public void ClearDownloadCache()
        {
            mWebCache.Clear();
        }

        /// <summary>
        /// ネットワーク上のテキストを取得します（同期処理）
        /// </summary>
        /// <param name="url">URL</param>
        public string GetTextFromNet(string url)
        {
            if (mWebCache.ContainsKey(url))
            {
                return mWebCache[url] as string;
            }

            var www = new UnityEngine.WWW(url);
            while (!www.isDone) { }
            mWebCache.Add(url, www.text);
            return www.text;
        }

        /// <summary>
        /// ネットワーク上のテキストを取得します（非同期処理）
        /// </summary>
        /// <param name="url">URL</param>
        /// <param name="callback">コールバック</param>
        public void GetTextFromNetAsync(string url, System.Action<string> callback)
        {
            mWebCache.Add(url, null);
            mGCInternal.StartCoroutine(_DownloadWebText(url, callback));
        }

        /// <summary>
        /// ネットワーク上の画像を取得し描画します
        /// </summary>
        /// <param name="url">画像のURL</param>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawOnlineImage(string url, float x, float y, sbyte priority = 0)
        {
            if (url == null || url.IndexOf("http") != 0)
            {
                UDebug.LogWarning("無効なURLです");
                return;
            }

            _DrawOnlineSprite(url, x, y, 1f, 1f, priority);
        }

        /// <summary>
        /// ネットワーク上の画像を取得し大きさを変えて描画します
        /// </summary>
        /// <param name="url">画像のURL</param>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="scaleX">横の拡縮率</param>
        /// <param name="scaleY">縦の拡縮率</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawScaledOnlineImage(string url, float x, float y, float scaleX, float scaleY, sbyte priority = 0)
        {
            if (url == null || url.IndexOf("http") != 0)
            {
                UDebug.LogWarning("無効なURLです");
                return;
            }

            _DrawOnlineSprite(url, x, y, scaleX, scaleY, priority);
        }

        /// <summary>
        /// ネットワーク上の画像を取得し回転させて描画します
        /// </summary>
        /// <param name="url">画像のURL</param>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="angle">回転角度 (度数法)</param>
        /// <param name="rotationX">画像左上を原点としたときの回転の中心位置X</param>
        /// <param name="rotationY">画像左上を原点としたときの回転の中心位置Y</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawRotatedOnlineImage(string url, float x, float y, float angle, float rotationX = 0f, float rotationY = 0f, sbyte priority = 0)
        {
            if (url == null || url.IndexOf("http") != 0)
            {
                UDebug.LogWarning("無効なURLです");
                return;
            }

            _DrawOnlineSprite(url, x, y, 1f, 1f, angle, rotationX, rotationY, 0f, 0f, 0f, 0f, priority);
        }

        /// <summary>
        /// ネットワーク上の画像を取得し一部分を切り取って描画します
        /// </summary>
        /// <param name="url">画像のURL</param>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="clipTop">画像上側の切り取る縦幅</param>
        /// <param name="clipRight">画像右側の切り取る横幅</param>
        /// <param name="clipBottom">画像下側の切り取る縦幅</param>
        /// <param name="clipLeft">画像左側の切り取る横幅</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawClippedOnlineImage(string url, float x, float y, float clipTop, float clipRight, float clipBottom, float clipLeft, sbyte priority = 0)
        {
            if (url == null || url.IndexOf("http") != 0)
            {
                UDebug.LogWarning("無効なURLです");
                return;
            }

            _DrawOnlineSprite(url, x, y, 0f, 0f, 0f, 0f, 0f, clipTop, clipRight, clipBottom, clipLeft, priority);
        }

        /// <summary>
        /// ネットワーク上の画像を取得し位置・拡縮率・回転角度を指定して描画します
        /// </summary>
        /// <param name="url">画像のURL</param>
        /// <param name="x">X座標</param>
        /// <param name="y">Y座標</param>
        /// <param name="scaleX">縦の拡縮率</param>
        /// <param name="scaleY">横の拡縮率</param>
        /// <param name="angle">回転角度 (度数法)</param>
        /// <param name="rotationX">画像左上を原点としたときの回転の中心位置X</param>
        /// <param name="rotationY">画像左上を原点としたときの回転の中心位置Y</param>
        /// <param name="priority">描画優先度 (-128～127)</param>
        public void DrawOnlineImageSRT(string url, float x, float y, float scaleX, float scaleY, float angle, float rotationX = 0f, float rotationY = 0f, sbyte priority = 0)
        {
            if (url == null || url.IndexOf("http") != 0)
            {
                UDebug.LogWarning("無効なURLです");
                return;
            }

            _DrawOnlineSprite(url, x, y, scaleX, scaleY, angle, rotationX, rotationY, 0f, 0f, 0f, 0f, priority);
        }

        /// <summary>
        /// 指定されたオンライン画像の横幅を返します。画像がダウンロード済みでない場合は 0 を返します
        /// </summary>
        /// <param name="url">画像のURL</param>
        /// <returns>指定された画像の横幅</returns>
        public int GetOnlineImageWidth(string url)
        {
            if (!mWebCache.ContainsKey(url)) return 0;

            var sprite = mWebCache[url] as USprite;
            if (sprite == null) return 0;

            return (int)sprite.rect.width;
        }

        /// <summary>
        /// 指定されたオンライン画像の高さを返します。画像がダウンロード済みでない場合は 0 を返します
        /// </summary>
        /// <param name="url">画像のURL</param>
        /// <returns>指定された画像の高さ</returns>
        public int GetOnlineImageHeight(string url)
        {
            if (!mWebCache.ContainsKey(url)) return 0;

            var sprite = mWebCache[url] as USprite;
            if (sprite == null) return 0;

            return (int)sprite.rect.height;
        }

        /// <summary>
        /// 指定されたオンライン画像がダウンロード済みかどうか
        /// </summary>
        /// <param name="url">画像のURL</param>
        public bool isDownloadedImage(string url)
        {
            if (!mWebCache.ContainsKey(url)) return false;

            var sprite = mWebCache[url] as USprite;
            return sprite != null;
        }

        #region 廃止された関数

        [System.Obsolete("DrawOnlineImage() に名称変更されました")]
        public void DrawImageFromNet(string url, float x, float y, sbyte priority = 0)
        {
            DrawOnlineImage(url, x, y, priority);
        }

        [System.Obsolete("DrawScaledOnlineImage() に名称変更されました")]
        public void DrawScaledImageFromNet(string url, float x, float y, float scaleH, float scaleV, sbyte priority = 0)
        {
            DrawScaledOnlineImage(url, x, y, scaleH, scaleV, priority);
        }

        [System.Obsolete("DrawRotatedOnlineImage() に名称変更されました")]
        public void DrawRotatedImageFromNet(string url, float x, float y, float angle, float rotationX = 0f, float rotationY = 0f, sbyte priority = 0)
        {
            DrawRotatedOnlineImage(url, x, y, angle, rotationX, rotationY, priority);
        }

        [System.Obsolete("DrawClippedOnlineImage() に名称変更されました")]
        public void DrawClippedImageFromNet(string url, float x, float y, float clipTop, float clipRight, float clipBottom, float clipLeft, sbyte priority = 0)
        {
            DrawClippedOnlineImage(url, x, y, clipTop, clipRight, clipBottom, clipLeft, priority);
        }

        [System.Obsolete("DrawOnlineImageSRT() に名称変更されました")]
        public void DrawImageSRTFromNet(string url, float x, float y, float scaleH, float scaleV, float angle, float rotationX = 0f, float rotationY = 0f, sbyte priority = 0)
        {
            DrawOnlineImageSRT(url, x, y, scaleH, scaleV, angle, rotationX, rotationY, priority);
        }

        [System.Obsolete("DrawClippedImageSRTFromNet() は廃止されました", true)]
        public void DrawClippedImageSRTFromNet(string url, float x, float y, float clipTop, float clipRight, float clipBottom, float clipLeft, float scaleH, float scaleV, float angle, float rotationX = 0f, float rotationY = 0f) { }

        #endregion

        private void _DrawOnlineSprite(string url, float x, float y, float scaleX, float scaleY, sbyte priority)
        {
            _DrawOnlineSprite(url, x, y, scaleX, scaleY, 0f, 0f, 0f, 0f, 0f, 0f, 0f, priority);
        }

        private void _DrawOnlineSprite(string url, float x, float y, float scaleX, float scaleY, float angle, float rotationX, float rotationY, float clipTop, float clipRight, float clipBottom, float clipLeft, sbyte priority)
        {
            if (!mWebCache.ContainsKey(url))
            {
                // ダウンロード
                mWebCache.Add(url, null);
                mGCInternal.StartCoroutine(_DownloadWebImage(url));
                return;
            }

            if (mWebCache[url] == null)
            {
                // ダウンロード完了待ち
                return;
            }

            if (scaleX == 0 || scaleY == 0) return;

            var sprite = mWebCache[url] as USprite;
            if (sprite == null)
            {
                return;
            }

            _DrawSprite(sprite, cColorWhite, x, y, scaleX, scaleY, angle, rotationX, rotationY, clipTop, clipRight, clipBottom, clipLeft, priority, true);
        }

        private System.Collections.IEnumerator _DownloadWebText(string url, System.Action<string> callback)
        {
            if (mWebCache.ContainsKey(url))
            {
                callback(mWebCache[url] as string);
                yield return null;
            }
            else
            {
                var www = new UnityEngine.WWW(url);
                yield return www;

                mWebCache[url] = www.text;
                callback(www.text);
            }
        }

        private System.Collections.IEnumerator _DownloadWebImage(string url)
        {
            var www = new UnityEngine.WWW(url);
            yield return www;

            var texture = www.texture;
            if (texture == null)
            {
                UDebug.LogWarningFormat("{0} からの画像ダウンロードに失敗しました", www.url);
                mWebCache.Remove(url);
            }

            mWebCache[url] = USprite.Create(texture, new URect(0, 0, texture.width, texture.height), new UVec2(0f, 1f), 1f);
        }

        #endregion

        #region UnityGC：ネットワークAPI (WebSocket)

        /// <summary>
        /// WebSocketがサーバーと接続状態にあるかどうか
        /// </summary>
        public bool isOpenWS { get { return mWs != null && mWs.ReadyState == WebSocketSharp.WebSocketState.Open; } }

        /// <summary>
        /// WebSocketサーバーに接続します
        /// </summary>
        /// <param name="url">WebSocketサーバーのURL</param>
        /// <param name="onOpen">WebSocketサーバーに接続したときに呼ばれる関数</param>
        /// <param name="onMessage">WebSocketサーバーからのメッセージを受け取る関数</param>
        /// <param name="onClose">WebSocketサーバーから切断したときに呼ばれる関数</param>
        /// <param name="onError">WebSocketサーバーとの接続でエラーが発生したときに呼ばれる関数</param>
        public void OpenWS(string url, Function onOpen = null, System.Action<string> onMessage = null, Function onClose = null, System.Action<string> onError = null)
        {
            if (isOpenWS)
            {
                mWs.Close(WebSocketSharp.CloseStatusCode.Away);
                mWs = null;
            }

            mWs = new WebSocketSharp.WebSocket(url);
            mWs.SslConfiguration.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => { return true; };

            if (onOpen != null) { mWs.OnOpen += (sender, e) => onOpen.Invoke(); }
            if (onMessage != null) { mWs.OnMessage += (sender, e) => onMessage.Invoke(e.IsText ? e.Data : null); }
            if (onError != null) { mWs.OnError += (sender, e) => onError.Invoke(e.Message); }
            mWs.OnClose += (sender, e) => {
                if (onClose != null) onClose.Invoke();
                mWs = null;
            };

            mWs.ConnectAsync();
        }

        /// <summary>
        /// WebSocketサーバーから切断します
        /// </summary>
        public void CloseWS()
        {
            if (!isOpenWS) return;

            mWs.Close(WebSocketSharp.CloseStatusCode.Normal);
            mWs = null;
        }

        /// <summary>
        /// WebSocketサーバーにメッセージを送信します
        /// </summary>
        /// <param name="message">メッセージ</param>
        public void SendWS(string message)
        {
            if (!isOpenWS) return;

            mWs.SendAsync(message, null);
        }

        /// <summary>
        /// WebSocketサーバーにメッセージを送信します
        /// </summary>
        /// <param name="obj">メッセージオブジェクト</param>
        public void SendWS(object obj)
        {
            SendWS(ConvertToJson(obj));
        }

        #endregion

        #region UnityGC：永続化API

        /// <summary>
        /// セーブデータから整数値を取り出します。存在しなかった場合 0 を返します
        /// </summary>
        /// <param name="key">キー</param>
        public int LoadAsInt(string key)
        {
            var value = Load(key);

            if (!string.IsNullOrEmpty(value))
            {
                int number;
                if (int.TryParse(value, out number))
                {
                    // 整数値が存在した
                    return number;
                }
            }

            // 存在しない または 整数値ではなかった
            return 0;
        }

        /// <summary>
        /// セーブデータから数値を取り出します。存在しなかった場合 0 を返します
        /// </summary>
        /// <param name="key">キー</param>
        public float LoadAsNumber(string key)
        {
            var value = Load(key);

            if (!string.IsNullOrEmpty(value))
            {
                float number;
                if (float.TryParse(value, out number))
                {
                    // 数値が存在した
                    return number;
                }
            }

            // 存在しない または 数値ではなかった
            return 0f;
        }

        /// <summary>
        /// セーブデータから文字列を取り出します。存在しなかった場合 null を返します
        /// </summary>
        /// <param name="key">キー</param>
        public string Load(string key)
        {
            string value;
            if (mSaveData.TryGetValue(key, out value))
            {
                return value;
            }

            return null;
        }

        /// <summary>
        /// セーブデータに整数値を追加します
        /// </summary>
        /// <param name="key">キー</param>
        /// <param name="value">保存する整数値</param>
        public void SaveAsInt(string key, int value)
        {
            Save(key, value.ToString());
        }

        /// <summary>
        /// セーブデータに数値を追加します
        /// </summary>
        /// <param name="key">キー</param>
        /// <param name="value">保存する数値</param>
        public void SaveAsNumber(string key, float value)
        {
            Save(key, value.ToString());
        }

        /// <summary>
        /// セーブデータに文字列を追加します
        /// </summary>
        /// <param name="key">キー</param>
        /// <param name="value">保存する文字列</param>
        public void Save(string key, string value)
        {
            mSaveData.Add(key, value, true);
        }

        /// <summary>
        /// セーブデータから指定されたキーのデータを削除します
        /// </summary>
        /// <param name="key">キー</param>
        public void DeleteData(string key)
        {
            mSaveData.Remove(key);
        }

        /// <summary>
        /// セーブデータを空にします
        /// </summary>
        public void DeleteDataAll()
        {
            mSaveData.Clear();
        }

        /// <summary>
        /// ストレージからセーブデータを読み取ります。この関数はゲーム起動時に自動で実行されます
        /// </summary>
        public void ReadDataByStorage()
        {
            var path = UnityEngine.Application.persistentDataPath;
            var fileName = "save.txt";
            var filePath = System.IO.Path.Combine(path, fileName);

            if (System.IO.File.Exists(filePath))
            {
                var json = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                mSaveData = SerializableDictionary<string, string>.FromJson(json);
            }

            if (mSaveData == null)
            {
                mSaveData = new SerializableDictionary<string, string>();
            }
        }

        /// <summary>
        /// ストレージにセーブデータを書き込みます
        /// </summary>
        public void WriteDataToStorage()
        {
            var path = UnityEngine.Application.persistentDataPath;
            var fileName = "save.txt";
            var filePath = System.IO.Path.Combine(path, fileName);

            var json = mSaveData.ToJson();
            System.IO.File.WriteAllText(filePath, json, System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// オブジェクトをJSON形式の文字列に変換します
        /// </summary>
        /// <param name="obj">オブジェクト</param>
        /// <returns>JSON形式の文字列</returns>
        public string ConvertToJson(object obj)
        {
            return UnityEngine.JsonUtility.ToJson(obj);
        }

        /// <summary>
        /// JSON形式の文字列からオブジェクトを復元します
        /// </summary>
        /// <typeparam name="T">復元するオブジェクトの型</typeparam>
        /// <param name="json">JSON形式の文字列</param>
        /// <returns>復元されたオブジェクト</returns>
        public T ConvertFromJson<T>(string json)
        {
            return UnityEngine.JsonUtility.FromJson<T>(json);
        }

        #endregion

        #region UnityGC：衝突判定API

        /// <summary>
        /// 2つの矩形が衝突しているかどうかを調べます
        /// </summary>
        /// <param name="x1">矩形1のX座標</param>
        /// <param name="y1">矩形1のY座標</param>
        /// <param name="w1">矩形1の横幅</param>
        /// <param name="h1">矩形1の縦幅</param>
        /// <param name="x2">矩形2のX座標</param>
        /// <param name="y2">矩形2のY座標</param>
        /// <param name="w2">矩形2の横幅</param>
        /// <param name="h2">矩形2の縦幅</param>
        /// <returns>衝突しているかどうか</returns>
        public bool CheckHitRect(float x1, float y1, float w1, float h1, float x2, float y2, float w2, float h2)
        {
            var a = new URect(x1, y1, w1, h1);
            var b = new URect(x2, y2, w2, h2);
            return a.Overlaps(b);
        }

        /// <summary>
        /// 2つの円が衝突しているかどうかを調べます
        /// </summary>
        /// <param name="x1">円1のX座標</param>
        /// <param name="y1">円1のY座標</param>
        /// <param name="r1">円1の半径</param>
        /// <param name="x2">円2のX座標</param>
        /// <param name="y2">円2のY座標</param>
        /// <param name="r2">円2の半径</param>
        /// <returns>衝突しているかどうか</returns>
        public bool CheckHitCircle(float x1, float y1, float r1, float x2, float y2, float r2)
        {
            var a = new UVec2(x1, y1);
            var b = new UVec2(x2, y2);
            return UVec2.Distance(a, b) <= r1 + r2;
        }

        /// <summary>
        /// 2つの画像が衝突しているかどうかを調べます
        /// </summary>
        /// <param name="img1">画像1のID</param>
        /// <param name="x1">画像1のX座標</param>
        /// <param name="y1">画像1のY座標</param>
        /// <param name="img2">画像2のID</param>
        /// <param name="x2">画像2のX座標</param>
        /// <param name="y2">画像2のY座標</param>
        /// <returns>衝突しているかどうか</returns>
        public bool CheckHitImage(int img1, float x1, float y1, int img2, float x2, float y2)
        {
            var w1 = GetImageWidth(img1);
            var h1 = GetImageHeight(img1);
            var w2 = GetImageWidth(img2);
            var h2 = GetImageHeight(img2);
            return CheckHitRect(x1, y1, w1, h1, x2, y2, w2, h2);
        }

        #endregion

        #region UnityGC：数学API

        /// <summary>
        /// cosを求めます
        /// </summary>
        /// <param name="angle">角度（度数法）</param>
        /// <returns>計算結果</returns>
        public float Cos(float angle)
        {
            return UnityEngine.Mathf.Cos(angle * UnityEngine.Mathf.Deg2Rad);
        }

        /// <summary>
        /// sinを求めます
        /// </summary>
        /// <param name="angle">角度（度数法）</param>
        /// <returns>計算結果</returns>
        public float Sin(float angle)
        {
            return UnityEngine.Mathf.Sin(angle * UnityEngine.Mathf.Deg2Rad);
        }

        /// <summary>
        /// atan2 あるいは ベクトルの角度を求めます
        /// </summary>
        /// <param name="x">X</param>
        /// <param name="y">Y</param>
        /// <returns>角度（度数法）</returns>
        public float Atan2(float x, float y)
        {
            return UnityEngine.Mathf.Atan2(y, x) * UnityEngine.Mathf.Rad2Deg;
        }

        /// <summary>
        /// 角度を度数法から弧度法に変換します
        /// </summary>
        /// <param name="degree">角度（度数法）</param>
        /// <returns>角度（弧度法）</returns>
        public float Deg2Rad(float degree)
        {
            return degree * UnityEngine.Mathf.Deg2Rad;
        }

        /// <summary>
        /// 角度を弧度法から度数法に変換します
        /// </summary>
        /// <param name="radian">角度（弧度法）</param>
        /// <returns>角度（度数法）</returns>
        public float Rad2Deg(float radian)
        {
            return radian * UnityEngine.Mathf.Rad2Deg;
        }

        /// <summary>
        /// min 以上 max 以下のランダムな整数値を返します
        /// </summary>
        /// <param name="min">最小の数</param>
        /// <param name="max">最大の数</param>
        /// <returns></returns>
        public int Random(int min, int max)
        {
            return UnityEngine.Mathf.FloorToInt(UnityEngine.Random.Range(min, max + 1));
        }

        /// <summary>
        /// 0 以上 1 以下のランダムな数値を返します
        /// </summary>
        /// <returns></returns>
        public float Random()
        {
            return UnityEngine.Random.value;
        }

        /// <summary>
        /// min 以上 max 以下のランダムな数値を返します
        /// </summary>
        /// <param name="min">最小の数</param>
        /// <param name="max">最大の数</param>
        /// <returns></returns>
        public float Random(float min, float max)
        {
            return UnityEngine.Random.Range(min, max);
        }

        #endregion

        #region UnityGC：時間API

        /// <summary>
        /// ゲームが起動してから現在のフレームまでの経過秒数
        /// </summary>
        public float time
        {
            get { return UnityEngine.Time.time; }
        }

        /// <summary>
        /// 前回のフレームから現在のフレームまでの経過秒数
        /// </summary>
        public float deltaTime
        {
            get { return UnityEngine.Time.deltaTime; }
        }

        /// <summary>
        /// 今日が西暦何年かを 0～9999 の数値で返します
        /// </summary>
        public int GetYear()
        {
            return System.DateTime.Now.Year;
        }

        /// <summary>
        /// 今日が何月かを 1～12 の数値で返します
        /// </summary>
        public int GetMonth()
        {
            return System.DateTime.Now.Month;
        }

        /// <summary>
        /// 今日の日付を 1～31 の数値で返します
        /// </summary>
        /// <returns></returns>
        public int GetDay()
        {
            return System.DateTime.Now.Day;
        }

        /// <summary>
        /// 今日の曜日を 0～6 の数値で返します。0が日曜日、6が土曜日です
        /// </summary>
        public int GetDayOfWeek()
        {
            return (int)System.DateTime.Now.DayOfWeek;
        }

        /// <summary>
        /// 今日の曜日を 月火水木金土日 の漢字で返します
        /// </summary>
        public string GetDayOfWeekKanji()
        {
            switch (System.DateTime.Now.DayOfWeek)
            {
                case System.DayOfWeek.Sunday: return "日";
                case System.DayOfWeek.Monday: return "月";
                case System.DayOfWeek.Tuesday: return "火";
                case System.DayOfWeek.Wednesday: return "水";
                case System.DayOfWeek.Thursday: return "木";
                case System.DayOfWeek.Friday: return "金";
                case System.DayOfWeek.Saturday: return "土";
                default: return "？";
            }
        }

        /// <summary>
        /// いま何時かを 0～23 の数値で返します
        /// </summary>
        public int GetHour()
        {
            return System.DateTime.Now.Hour;
        }

        /// <summary>
        /// いま何分かを 0～59 の数値で返します
        /// </summary>
        public int GetMinute()
        {
            return System.DateTime.Now.Minute;
        }

        /// <summary>
        /// いま何秒かを 0～59 の数値で返します
        /// </summary>
        public int GetSecond()
        {
            return System.DateTime.Now.Second;
        }

        /// <summary>
        /// いま何ミリ秒かを 0～999 で返します
        /// </summary>
        /// <returns></returns>
        public int GetMilliSecond()
        {
            return System.DateTime.Now.Millisecond;
        }

        #endregion

        #region UnityGC：デバッグAPI

        /// <summary>
        /// デバッグ環境あるいはデバッグビルドで実行されている場合に真を返します
        /// </summary>
        public bool isDevelop
        {
            get
            {
                return UnityEngine.Application.platform == UnityEngine.RuntimePlatform.WindowsEditor || UnityEngine.Application.platform == UnityEngine.RuntimePlatform.OSXEditor || UDebug.isDebugBuild;
            }
        }

        /// <summary>
        /// コンソールにログメッセージを出力します。この関数は isDevelop が真の時のみ動作します
        /// </summary>
        /// <param name="message">ログメッセージ</param>
        public void Trace(string message)
        {
            if (isDevelop)
            {
                UDebug.Log(message);
            }
        }

        /// <summary>
        /// コンソールにベクトル値を出力します。この関数は isDevelop が真の時のみ動作します
        /// </summary>
        /// <param name="value">ベクトル値</param>
        public void Trace(UVec2 value)
        {
            Trace(string.Format("x: {0}, y: {1}", value.x, value.y));
        }

        /// <summary>
        /// コンソールに数値を出力します。この関数は isDevelop が真の時のみ動作します
        /// </summary>
        /// <param name="value">数値</param>
        public void Trace(System.IComparable value)
        {
            Trace(value.ToString());
        }

        /// <summary>
        /// コンソールに真偽値を出力します。この関数は isDevelop が真の時のみ動作します
        /// </summary>
        /// <param name="value">真偽値</param>
        public void Trace(bool value)
        {
            Trace(value ? "True" : "False");
        }

        /// <summary>
        /// 指定したキーが押されているかどうか。この関数は isDevelop が真の時のみ動作します
        /// </summary>
        /// <param name="key">調べたいキー</param>
        /// <returns>押されているかどうか</returns>
        public bool GetIsKeyPress(string key)
        {
            return isDevelop && UnityEngine.Input.GetKey(key);
        }

        /// <summary>
        /// 指定したキーが押された瞬間かどうか。この関数は isDevelop が真の時のみ動作します
        /// </summary>
        /// <param name="key">調べたいキー</param>
        /// <returns>押された瞬間かどうか</returns>
        public bool GetIsKeyPushed(string key)
        {
            return isDevelop && UnityEngine.Input.GetKeyDown(key);
        }

        /// <summary>
        /// 指定したキーが離された瞬間かどうか。この関数は isDevelop が真の時のみ動作します
        /// </summary>
        /// <param name="key">調べたいキー</param>
        /// <returns>離された瞬間かどうか</returns>
        public bool GetIsKeyReleased(string key)
        {
            return isDevelop && UnityEngine.Input.GetKeyUp(key);
        }

        #endregion

        #region UnityGC：その他のAPI

        /// <summary>
        /// 画像・音声の読み込みが終わっているかどうか
        /// </summary>
        public bool isLoaded
        {
            get { return mIsLoaded; }
        }

        /// <summary>
        /// アプリケーションを終了します
        /// </summary>
        public void ExitApp()
        {
            UnityEngine.Application.Quit();
        }

        #endregion
        
        #endregion
    }
}
