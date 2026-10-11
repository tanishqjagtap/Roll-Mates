using UnityEngine;
using UnityEngine.SceneManagement;

public class BalanceSystem : MonoBehaviour
{
    public LogController log;
    public FollowCamera cam;            // optional, for shake

    [Header("Sensors")]
    public Transform leftSensor;
    public Transform rightSensor;
    public float dipDepth = 0.5f;       // how far below the water line counts as "under"
    public float timeToFall = 2f;
    public float recoverRate = 1f;      // timer drains this fast when the end comes back up
    public bool oppositePlayerFalls = true;
    public float secondFallDelay = 1.2f;

    public bool LeftFell { get; private set; }
    public bool RightFell { get; private set; }
    public bool GameOver { get; private set; }

    float leftTimer, rightTimer, secondFallTimer;
    LogFloat water;

    void Awake() => water = log.GetComponent<LogFloat>();

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }
        if (GameOver) return;

        if (!LeftFell && !RightFell) CheckSensors();
        else SecondFall();
    }

    bool IsUnder(Transform sensor)
    {
        // only the sensor's distance along the log's length matters, so rolling can't fool it
        float z = sensor.localPosition.z;
        Vector3 p = log.transform.position + log.transform.forward * z;
        return p.y < water.waterLevel - dipDepth;
    }

    void CheckSensors()
    {
        float dt = Time.deltaTime;
        leftTimer = IsUnder(leftSensor) ? leftTimer + dt : Mathf.Max(0f, leftTimer - dt * recoverRate);
        rightTimer = IsUnder(rightSensor) ? rightTimer + dt : Mathf.Max(0f, rightTimer - dt * recoverRate);

        if (leftTimer >= timeToFall) FirstFall(oppositePlayerFalls ? false : true);   // true = left player falls
        else if (rightTimer >= timeToFall) FirstFall(oppositePlayerFalls ? true : false);
    }

    void FirstFall(bool leftPlayerFalls)
    {
        if (leftPlayerFalls) { LeftFell = true; log.leftEnabled = false; }
        else { RightFell = true; log.rightEnabled = false; }
        secondFallTimer = secondFallDelay;
        if (cam != null) cam.Shake(0.4f, 0.5f);
        Debug.Log((leftPlayerFalls ? "LEFT" : "RIGHT") + " player fell");
    }

    void SecondFall()
    {
        secondFallTimer -= Time.deltaTime;
        if (secondFallTimer > 0f) return;
        LeftFell = RightFell = true;
        log.leftEnabled = log.rightEnabled = false;
        GameOver = true;
        if (cam != null) cam.Shake(0.6f, 0.7f);
        Debug.Log("GAME OVER");
    }

    // temporary debug bars: fill = how close that end is to triggering a fall
    void OnGUI()
    {
        DrawBar(20, 20, "LEFT END (W side)", leftTimer / timeToFall, LeftFell);
        DrawBar(20, 70, "RIGHT END (Up side)", rightTimer / timeToFall, RightFell);
        if (GameOver) GUI.Label(new Rect(20, 120, 300, 30), "GAME OVER - press R");
    }

    void DrawBar(float x, float y, string label, float v, bool fell)
    {
        GUI.Label(new Rect(x, y, 250, 20), fell ? label + "  (player fell)" : label);
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(x, y + 22, 200, 14), Texture2D.whiteTexture);
        GUI.color = Color.Lerp(Color.green, Color.red, v);
        GUI.DrawTexture(new Rect(x, y + 22, 200 * Mathf.Clamp01(v), 14), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}