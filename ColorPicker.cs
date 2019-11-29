using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

// Color Picker는 한개의 창만 띄울 수 있기 때문에 Singleton으로 구현하였다.
public class UIColorPicker : Singleton<UIColorPicker> {

	/////////////////////////////////
	//
	// Private Variable
	//
	/////////////////////////////////

	struct RGB {
		public int r, g, b;

		public static implicit operator Color( RGB rgb ) {
			Color col;
			col.r = rgb.r / 255.0f;
			col.g = rgb.g / 255.0f;
			col.b = rgb.b / 255.0f;
			col.a = 1.0f;
			return col;
		}

		public static implicit operator RGB( Color col ) {
			RGB rgb;
			rgb.r = (int)( col.r * 255.0f );
			rgb.g = (int)( col.g * 255.0f );
			rgb.b = (int)( col.b * 255.0f );
			return rgb;
		}
	}

	struct HSV {
		int		m_Hue;
		float	m_Saturation;
		float	m_Value;

		public int		h { get { return m_Hue; } set { m_Hue = value % 360; } }
		public float	s { get { return m_Saturation; } set { m_Saturation = value; } }
		public float	v { get { return m_Value; } set { m_Value = value; } }

		public HSV( int h, float s, float v ) {
			m_Hue = h%360;
			m_Saturation = s;
			m_Value = v;
		}

		public static implicit operator Color( HSV hsv ) {
			float c = hsv.v * hsv.s;
			float hIndex = hsv.h / 60.0f;
			float x = c * ( 1.0f - Math.Abs(hIndex % 2.0f - 1.0f) );
			float r=0.0f, g=0.0f, b=0.0f;
			switch((int)hIndex) {
				case 0:
					r = c;
					g = x;
					break;
				case 1:
					r = x;
					g = c;
					break;
				case 2:
					g = c;
					b = x;
					break;
				case 3:
					g = x;
					b = c;
					break;
				case 4:
					r = x;
					b = c;
					break;
				case 5:
					r = c;
					b = x;
					break;
			}
			float m = hsv.v - c;
			Color col = new Color(r + m, g + m, b + m, 1.0f);
			return col;
		}

		public static implicit operator HSV( Color col ) {
			HSV hsv = new HSV();

			float M = Mathf.Max(Math.Max(col.r, col.g), col.b);
			float m = Mathf.Min(Math.Min(col.r, col.g), col.b);
			float c = M - m;
			float hIndex = 0.0f;
			if(c == 0.0f)
				hIndex = 0.0f;
			else if(M == col.r)
				hIndex = ( ( col.g - col.b ) / c ) % 6.0f;
			else if(M == col.g)
				hIndex = ( col.b - col.r ) / c + 2.0f;
			else if(M == col.b)
				hIndex = ( col.r - col.g ) / c + 4.0f;
			hsv.h = (int)(hIndex * 60);
			hsv.v = M;
			if(hsv.v == 0.0f)
				hsv.s = 0.0f;
			else
				hsv.s = c / hsv.v;
			return hsv;
		}
	}
	
	// SV, H좌표계의 이미지
	[SerializeField] RawImage		m_SVImage;
	[SerializeField] RawImage[]		m_HImageArray;
	// 과거, 현재 색상의 이미지
	[SerializeField] Image			m_NewColorImage;
	[SerializeField] Image			m_CurrentColorImage;
	// HSV, RGB, HEX패널의 인풋
	[SerializeField] InputField[]	m_HSVInputFieldArray;
	[SerializeField] InputField[]	m_RGBInputFieldArray;
	[SerializeField] InputField		m_HexInputField;
	// 각 좌표계의 포인터 transform
	[SerializeField] RectTransform  m_SVPointer;
	[SerializeField] RectTransform	m_HPointer;
	[SerializeField] RectTransform	m_HTransform;
	[SerializeField] Shader			m_TextureShader;
	[SerializeField] RectTransform	m_GroupTransform;

	// SV좌표계의 크기와 H좌표계의 높이
	Vector2			m_SVSize;
	float			m_HHeight;
	// 과거, 현재의 HSV값
	HSV				m_NewHSV;
	HSV				m_CurrentHSV;
	// 색상이 변경되면 알려줄 callback함수
	Action<Color>	m_fChangeColor;
	// H좌표계에 들어갈 render texture
	RenderTexture[] m_HRTArray;
	Material		m_Material;
	Vector3[]		m_Vertices;
	Texture2D		m_SVTexture;
	RectTransform	m_SVTrnasform;
	// 창 이동을 위한 변수
	Vector2			m_MoveInterval;
	RectTransform	m_CanvasTransform;
	bool			m_MoveState;
	

	/////////////////////////////////
	//
	// Property
	//
	/////////////////////////////////
	
	// 각 값들이 해당 InputField에 연결되어 있다.
	public string hue {
		set {
			int num = int.Parse(value);
			if(num < 0) {
				num = 0;
				m_HSVInputFieldArray[0].text = "0";
			}
			else if(num > 360) {
				num = 359;
				m_HSVInputFieldArray[0].text = "359";
			}
			m_NewHSV.h = num;
			RefreshSVSpace();
			RefreshColor();
		}
	}

	public string saturation {
		set {
			int num = int.Parse(value);
			if(num < 0) {
				num = 0;
				m_HSVInputFieldArray[1].text = "0";
			}
			else if(num > 100) {
				num = 100;
				m_HSVInputFieldArray[1].text = "100";
			}
			m_NewHSV.s = num / 100.0f;
			RefreshColor();
		}
	}

	public string value {
		set {
			int num = int.Parse(value);
			if(num < 0) {
				num = 0;
				m_HSVInputFieldArray[2].text = "0";
			}
			else if(num > 100) {
				num = 100;
				m_HSVInputFieldArray[2].text = "100";
			}
			m_NewHSV.v = num / 100.0f;
			RefreshColor();
		}
	}

	public string red {
		set {
			int num = int.Parse(value);
			if(num < 0) {
				num = 0;
				m_RGBInputFieldArray[0].text = "0";
			}
			else if(num > 255) {
				num = 255;
				m_RGBInputFieldArray[0].text = "255";
			}
			Color col = m_NewHSV;
			col.r = num / 255.0f;
			m_NewHSV = col;
			RefreshSVSpace();
			RefreshColor();
		}
	}

	public string green {
		set {
			int num = int.Parse(value);
			if(num < 0) {
				num = 0;
				m_RGBInputFieldArray[1].text = "0";
			}
			else if(num > 255) {
				num = 255;
				m_RGBInputFieldArray[1].text = "255";
			}
			Color col = m_NewHSV;
			col.g = num / 255.0f;
			m_NewHSV = col;
			RefreshSVSpace();
			RefreshColor();
		}
	}

	public string blue {
		set {
			int num = int.Parse(value);
			if(num < 0) {
				num = 0;
				m_RGBInputFieldArray[2].text = "0";
			}
			else if(num > 255) {
				num = 255;
				m_RGBInputFieldArray[2].text = "255";
			}
			Color col = m_NewHSV;
			col.b = num / 255.0f;
			m_NewHSV = col;
			RefreshSVSpace();
			RefreshColor();
		}
	}

	public string hex {
		set {
			var chaHex = value.ToCharArray();
			for(int i = 0; i < 6; ++i) {
				if(chaHex[i] >= 48 && chaHex[i] < 58)
					continue;
				if(chaHex[i] >= 97 && chaHex[i] < 103)
					continue;

				print((int)chaHex[i]);
				m_HexInputField.text = ColorToHex(m_CurrentHSV);
				return;
			}
			Color col = HexToColor(value);
			m_NewHSV = col;
			RefreshSVSpace();
			RefreshColor();
		}
	}
	// 현재 window의 bar를 클릭중이라면 이동가능한 상태가 된다.
	public bool moveState { 
		set { 
			m_MoveState = value;
			if(m_MoveState == true) {
				Vector2 point;
				RectTransformUtility.ScreenPointToLocalPointInRectangle(m_CanvasTransform, Input.mousePosition, null, out point);
				m_MoveInterval = point - m_GroupTransform.anchoredPosition;
			}
		} 
	}

	/////////////////////////////////
	//
	// Function
	//
	/////////////////////////////////
	
	// 초기화
	void Awake() {
		m_Material			= new Material(m_TextureShader);
		m_CanvasTransform	= GetComponent<RectTransform>();

		m_SVTrnasform		= m_SVImage.rectTransform;
		m_SVSize			= m_SVTrnasform.sizeDelta;
		m_HHeight			= m_SVSize.y;
		m_SVTexture			= new Texture2D((int)m_SVSize.x, (int)m_SVSize.y);
		m_SVTexture.name	= "SVTExture2D";
		m_SVImage.texture	= m_SVTexture;

		var hRect = m_HImageArray[0].rectTransform.rect;
		var hSize = new Vector2(hRect.width, hRect.height);
		m_Vertices = new Vector3[]{new Vector3(0.0f, 1.0f, 0.0f)
										, new Vector3(1.0f, 1.0f, 0.0f)
										, new Vector3(1.0f, 0.0f, 0.0f)
										, new Vector3(0.0f, 0.0f, 0.0f)};
		m_HRTArray = new RenderTexture[6];
		HSV upHsv = new HSV(360, 1.0f, 1.0f);
		HSV downHsv = new HSV(300, 1.0f, 1.0f);
		for(int i = 0; i < 6; ++i) {
			m_HRTArray[i] = new RenderTexture((int)hSize.x, (int)hSize.y, 0);
			m_HRTArray[i].name = "HRenderTexutre" + i;
			m_HRTArray[i].Create();
			m_HImageArray[i].texture = m_HRTArray[i];

			upHsv.h = 360 - 60 * i;
			downHsv.h = 300 - 60 * i;

			RenderTexture.active = m_HRTArray[i];
			m_Material.SetPass(0);
			GL.PushMatrix();
			GL.LoadOrtho();
			GL.Begin(GL.QUADS);
			for(int j = 0; j < 4; ++j) {
				if(j < 2)
					GL.Color(upHsv);
				else
					GL.Color(downHsv);
				GL.Vertex(m_Vertices[j]);
			}
			GL.End();
			GL.PopMatrix();
			RenderTexture.active = null;
		}
	}

	// Color Picker창을 팝업한다. 시작시 색상과 색상 변경시 알려줄 callback함수를 등록할 수 있다.
	public void Show( Color col, Action<Color> fChangeColor ) {
		gameObject.SetActive(true);

		m_CurrentHSV = col;
		m_NewHSV = col;
		m_fChangeColor = fChangeColor;
		m_CurrentColorImage.color = col;

		Vector2 svPosition = new Vector2(m_SVSize.x * m_CurrentHSV.s, -m_SVSize.y * (1.0f - m_CurrentHSV.v));
		m_SVPointer.anchoredPosition = svPosition;

		RefreshColor();
		RefreshSVSpace();
	}

	// 창이 이동중인지 확인한다.
	void Update() {
		if(m_MoveState == false)
			return;

		Vector2 point;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(m_CanvasTransform, Input.mousePosition, null, out point);
		m_GroupTransform.anchoredPosition = point - m_MoveInterval;
	}

	// 해당 좌표계를 클릭했는지 확인한다.
	public void ClickHSpace() {
		Vector2 point;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(m_HTransform, Input.mousePosition, null, out point);
		m_NewHSV.h = Mathf.Clamp((int)( 360.0f * ( 1.0f + point.y / m_HHeight )) , 0, 360);
		RefreshColor();
		RefreshSVSpace();
	}

	public void ClickSVSpace() {
		Vector2 point;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(m_SVTrnasform, Input.mousePosition, null, out point);
		m_NewHSV.s = Mathf.Clamp(point.x / m_SVSize.x, 0.0f, 1.0f);
		m_NewHSV.v = Mathf.Clamp(1.0f - ( -point.y / m_SVSize.y ), 0.0f, 1.0f);
		RefreshColor();
	}

	// 색상을 갱신한다.
	void RefreshColor() {
		Color col = m_NewHSV;
		m_NewColorImage.color = col;
		m_HSVInputFieldArray[0].text = m_NewHSV.h.ToString();
		m_HSVInputFieldArray[1].text = ( (int)( m_NewHSV.s * 100 ) ).ToString();
		m_HSVInputFieldArray[2].text = ( (int)( m_NewHSV.v * 100 ) ).ToString();
		m_RGBInputFieldArray[0].text = ( (int)( col.r * 255.0f ) ).ToString();
		m_RGBInputFieldArray[1].text = ( (int)( col.g * 255.0f ) ).ToString();
		m_RGBInputFieldArray[2].text = ( (int)( col.b * 255.0f ) ).ToString();
		m_HexInputField.text = ColorToHex(col);

		Vector2 point = new Vector2(m_NewHSV.s * m_SVSize.x, -(1.0f - m_NewHSV.v) * m_SVSize.y);
		m_SVPointer.anchoredPosition = point;

		m_fChangeColor(col);
	}

	// SV좌표계를 갱신한다.
	void RefreshSVSpace() {
		HSV hsv = new HSV(m_NewHSV.h, 1.0f, 1.0f);
		// Set Pixels
		int width = (int)m_SVSize.x;
		int height = (int)m_SVSize.y;
		for(int h = 0; h < height; ++h) {
			for(int w = 0; w < width; ++w) {
				hsv.s = (float)w / (width-1);
				hsv.v = (float)h / (height-1);
				Color col = hsv;
				m_SVTexture.SetPixel(w, h, col);
			}
		}
		m_SVTexture.Apply();

		float hRate = 1.0f - hsv.h / 360.0f;
		Vector2 point = new Vector2(0.0f, -m_HHeight * hRate);
		m_HPointer.anchoredPosition = point;
	}

	Color HexToColor( string hex ) {
		var chaHex = hex.ToCharArray();
		Color col;
		col.r = ( CharToInt(chaHex[0]) * 16 + CharToInt(chaHex[1]) ) / 255.0f;
		col.g = ( CharToInt(chaHex[2]) * 16 + CharToInt(chaHex[3]) ) / 255.0f;
		col.b = ( CharToInt(chaHex[4]) * 16 + CharToInt(chaHex[5]) ) / 255.0f;
		col.a = 1.0f;
		return col;
	}

	string ColorToHex( Color col ) {
		int r = (int)( col.r * 255.0f );
		int g = (int)( col.g * 255.0f );
		int b = (int)( col.b * 255.0f );
		string hex = "";
		hex += IntToChar(r / 16);
		hex += IntToChar(r % 16);
		hex += IntToChar(g / 16);
		hex += IntToChar(g % 16);
		hex += IntToChar(b / 16);
		hex += IntToChar(b % 16);
		return hex;
	}

	int CharToInt( char cha ) {
		int intCha = (int)cha;
		if(intCha < 58)
			return intCha - 48;
		else
			return intCha - 87;
	}

	char IntToChar( int num ) {
		if(num < 10)
			return (char)(48 + num);
		else
			return (char)(87 + num);
	}

	public void Save() {
		m_fChangeColor(m_NewHSV);
		gameObject.SetActive(false);
	}

	public void Cancel() {
		m_fChangeColor(m_CurrentHSV);
		gameObject.SetActive(false);
	}

}
