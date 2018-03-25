using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;// 使用domove方法
using UnityEngine.SceneManagement;//为切换场景
using UnityEngine.UI;// 使用UI的时候需引入此命名空间
using LeanCloud;
using UniRx;



public class Player : MonoBehaviour {
    // 定义跳跃系数
    public float Factor;

    // 要根据第一个盒子自动生成后面的盒子，而且距离也要随即变化，因此给一个最大距离，并获取第一个盒子
    public float MaxDistance = 5;
    public GameObject Stage;

    // 使相机跟随，要获得camera
    public Transform Camera;

    // 获取显示的分数
    public Text ScoreText;

    // 定义粒子特效,Particle:粒子
    public GameObject Particle;

    // 实现Player蓄力下蹲效果，先获取到Player的头和身体变量
    public Transform Head;
    public Transform Body;

    // 获取到头顶加分特效
    public Text SingleScoreText;


	// 定义三个进行分数上传、联网显示的
	public GameObject SaveScorePanel;
	public InputField Namefield;
	public Button SaveButton;

    public GameObject RankPanel;
    public GameObject RankItem;

    public Button RestartButton;

    // 定义一下camera的相对位置
    private Vector3 _cameraRelativePosition;

    // 首先定义下刚体组件
    private Rigidbody _rigidbody;
    private float _startTime;
    // 定义当前Player所在的盒子
    private GameObject _currentStage;
    // 记录上一次Player所停留的盒子，避免在同一个盒子上跳多下而生成多个盒子
    private Collider _lastCollisionCollider;
    // 定义分数这样一个变量
    private int _score;

    //
    private bool _isUpdateScoreAnimation;

    // 给盒子加上一个方向上的定义，并且初始化为x轴的正方向
    Vector3 _direction = new Vector3(1,0,0);

    // 
    private float _scoreAnimationStartTime;

	void Start () {
		// 然后在这里获取到刚才定义的刚体组件
        _rigidbody = GetComponent<Rigidbody>();

        // 由于此时Player的中心还是在中间，容易摔倒，因此把重心挪到Player底部，不容易摔倒
        _rigidbody.centerOfMass = new Vector3(0,0,0);
        
        // 当前位置就是所在的Stage
        _currentStage = Stage;

        // 游戏开始将上一个所在盒子初始化为当前盒子，即第一个盒子
        _lastCollisionCollider = _currentStage.GetComponent<Collider>();

        // 游戏一开始就生成一个盒子
        SpawnStage();

        // 一开始用相机的位置 - Player的位置，就得到相机的相对位置
        _cameraRelativePosition = Camera.position - transform.position;

		// 用脚本来绑定事件
		SaveButton.onClick.AddListener(OnClickSaveButton);

        RestartButton.onClick.AddListener(() => {
            SceneManager.LoadScene(0);
        });

        // 初始化一下
        MainThreadDispatcher.Initialize();
	}
	

	void Update () {
		// 由于这里是在电脑上运行，定义用空格来进行蓄力，所以要检测空格按压的时间
        // 空格按下的时间
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _startTime = Time.time;
            // 当按下空格开始，就播放粒子蓄力特效
            Particle.SetActive(true);
        }

        // 空格松开的时间
        if (Input.GetKeyUp(KeyCode.Space))
        {
            // 直接定义时间差值elapse
            var elapse = Time.time - _startTime;
            // 接着使用跳跃的方法，即一松开空格马上进行跳跃
            OnJump(elapse);
            // 松开按键，蓄力粒子特效也消失
            Particle.SetActive(false);

            // 调用插件函数动画进行形状还原，0.1f表示三维向量全部为0.1f，若三维不同可同时进行输入,0.2f为还原动作时间
            Body.transform.DOScale(0.1f,0.2f);
            // 0.3f为头部的初始位置
            Head.transform.DOLocalMoveY(0.3f,0.2f);

            // 盒子在形状和位置上进行还原
            _currentStage.transform.DOLocalMoveY(0.25f, 0.2f);
            _currentStage.transform.DOScale(new Vector3(1,0.5f,1), 0.2f);

        }

        // 上面两个getkeydown和up都是执行一次，下面这个是只要按住就每帧执行一次,实现蓄力下蹲效果
        if(Input.GetKey(KeyCode.Space)){
            // 身体是形状上x,z轴向变大，y轴变小，头部只是位置上y轴变小下降就OK，deltatime就是每帧渲染的时间
            // 0.03形变系数，后期调，因为形变是同一方向两端同时形变，所以头移动系数为形变系数的两倍
            Body.transform.localScale += new Vector3(1, -1, 1) * 0.03f * Time.deltaTime;
            Head.transform.localPosition += new Vector3(0, -1, 0) * 0.06f * Time.deltaTime;
            // 当然很重要的是跳出去之后Player形状要复原，也就是松开空格之后，见上面的if

            // 盒子也要进行缩放
            _currentStage.transform.localScale += new Vector3(0, -1, 0) * 0.09f *Time.deltaTime;
            // （注意盒子上下缩放的时候会离开地面，因此要同时向下进行移动）
            _currentStage.transform.localPosition += new Vector3(0, -1, 0) * 0.09f * Time.deltaTime;
        }

        if(_isUpdateScoreAnimation){
            UpdateScoreAnimation();
        }
	}

    // 定义一个跳跃的方法，形参为上面得到的时间按压差值
    void OnJump(float elapse) {
        // 要使得Player进行跳跃，只要给其刚体添加一个向前和向上的力即可,Factor为跳跃系数，后期调节
        // 同时最后一个参数，由于AddForce方法默认是持续的力，所以要改成impulse，瞬间的力,_direction为一个y轴上的向量
        _rigidbody.AddForce((new Vector3(0,1,0) + _direction) * elapse * Factor,ForceMode.Impulse);
    }

    // 随机生成盒子函数
    void SpawnStage() { 
        // 通过Instantiate来生成盒子
        var stage = Instantiate(Stage);

        // 接下来就是随机生成的盒子的位置,方向乘以距离，可以是x方向也可以z方向
        stage.transform.position = _currentStage.transform.position + _direction * Random.Range(1.1f, MaxDistance);

        // 实现盒子的随机大小，只需要随机x和z轴的大小即可
        var randomScale = Random.Range(0.5f,1);
        stage.transform.localScale = new Vector3(randomScale,0.5f,randomScale);

        // 实现盒子的随机颜色，通过renderer组件即可,0f是因为，如果不加f,默认就是整数，从而只能取到0（黑）
        stage.GetComponent<Renderer>().material.color = new Color(Random.Range(0f,1),Random.Range(0f,1),Random.Range(0f,1));

    }

    // Player跳到了下一个台子上才继续生成下下一个台子，因此需要一个判断方法
    void OnCollisionEnter(Collision collision) {
        // 若碰到了下一个台子，那么生成下一个台子 ， && 后面的是进行判断是否还是在该盒子上没离开
        if (collision.gameObject.name.Contains("Stage") && collision.collider != _lastCollisionCollider) {
            // 并且将上一个盒子定义为当前所在的盒子
            _lastCollisionCollider = collision.collider;

            // 先把当前的台子定义为判断条件里的物体
            _currentStage = collision.gameObject;

            // 随机盒子生成方向方法
            RandomDirection();

            // 生成下一个台子
            SpawnStage();

            // 并且相机跟随
            MoveCamera();

            // 显示头顶加分特效
            ShowScoreAnimation();

            // 并且总分数 +1
            _score++;
            // 分数加完之后给他显示出来
            ScoreText.text = _score.ToString();
        }

        // 碰撞检测，落地死
        if (collision.gameObject.name == "Ground") { 
            // 本局游戏结束并且重新开始，对于这种游戏来说比较简单，只要重新加载一下场景，即可
            // 0是在buildsetting里面当前场景的index值
			// 但是为了先显示游戏分数联网界面，我们先不重新加载场景
			SaveScorePanel.SetActive(true);
        }
    }

    // 定义加分特效函数
    private void ShowScoreAnimation() {
        _isUpdateScoreAnimation = true;
        _scoreAnimationStartTime = Time.time;
    }

    // 实时计算加分特效所在的位置
    void UpdateScoreAnimation() {
        if (Time.time - _scoreAnimationStartTime > 1)
            _isUpdateScoreAnimation = false;

        var playerScreenPos = RectTransformUtility.WorldToScreenPoint(Camera.GetComponent<Camera>(),transform.position);
        SingleScoreText.transform.position = playerScreenPos + Vector2.Lerp(Vector2.zero, new Vector2(0, 100), Time.time - _scoreAnimationStartTime);

        // 前三个0为rgb值，第四个0为α值，即透明状态
        SingleScoreText.color = Color.Lerp(Color.black, new Color(0, 0, 0, 0), Time.time - _scoreAnimationStartTime);    
    }

    void RandomDirection() { 
        // 首先定义一个方向种子，给其俩值：0 或者 1 ,注意random.range()不包含最后一个数
        var seed = Random.Range(0,2);
        // 0为x方向，1为z方向
        if (seed == 0)
        {
            _direction = new Vector3(1, 0, 0);
        }
        else {
            _direction = new Vector3(0, 0, 1);
        }
    }

    void MoveCamera() {
        // 使用插件进行移动，效果不突兀,1为动画时长
        Camera.DOMove(transform.position + _cameraRelativePosition,1);
    }

	void OnClickSaveButton(){
		var nickname = Namefield.text;

		AVObject gameScore = new AVObject ("GameScore");
		gameScore ["score"] = _score;
		gameScore ["playerName"] = nickname;
        gameScore.SaveAsync().ContinueWith(_ => {
            ShowRankPanel();
        });
        SaveScorePanel.SetActive(false);

	}

    // 显示排行榜UI
    void ShowRankPanel() {
        AVQuery<AVObject> query = new AVQuery<AVObject>("GameScore").OrderByDescending("score").Limit(10);
        query.FindAsync().ContinueWith(t => {
            var results = t.Result;
            var scores = new List<string>();

            foreach (var result in results) { 
                var score = result["playerName"] + ":" + result["score"];
                scores.Add(score);
            }

            // 将代码返回到主线程执行
            MainThreadDispatcher.Send(_ => {
                foreach (var score in scores) {
                    var item = Instantiate(RankItem);
                    item.SetActive(true);
                    item.GetComponent<Text>().text = score;
                    item.transform.SetParent(RankItem.transform.parent);
                }
                RankPanel.SetActive(true);
            },null);
        });
    }
}
