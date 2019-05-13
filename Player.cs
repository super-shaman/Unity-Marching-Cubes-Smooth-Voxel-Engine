using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{

    public Rigidbody rb;
    public GameObject cam;
    public Vector3 pos;
    public float Size;
    bool forward;
    bool left;
    bool back;
    bool right;
    bool up;
    bool down;
    float speed = 0.1f;
    bool locked;
    bool fly = true;
    bool onGround;
    bool run;
    bool running;
    float runTimer;
    Camera Cam;
    public Plane[] planes;

    // Start is called before the first frame update
    void Start()
    {
        Cam = cam.GetComponent<Camera>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        locked = true;
    }

    void Break()
    {
        RaycastHit hit;
        if (Physics.Raycast(cam.transform.position, Quaternion.Euler(new Vector3(-rx, ry, 0))* new Vector3(0,0,1), out hit))
        {
            VoxelChunk c = hit.transform.gameObject.GetComponent<VoxelChunk>();
            if (c != null)
            {
                c.Break(hit.point, Size);
            }
        }
    }
    void Place()
    {
        RaycastHit hit;
        if (Physics.Raycast(cam.transform.position, Quaternion.Euler(new Vector3(-rx, ry, 0)) * new Vector3(0, 0, 1), out hit))
        {
            VoxelChunk c = hit.transform.gameObject.GetComponent<VoxelChunk>();
            if (c != null)
            {
                c.Place(hit.point, Size);
            }
        }
    }

    float rx;
    float ry;

    // Update is called once per frame
    void Update()
    {
        planes = GeometryUtility.CalculateFrustumPlanes(Cam);
        if (locked && Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            locked = false;
        }
        if (Input.GetMouseButtonDown(0))
        {
            if (!locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                locked = true;
            }
        }
        if (Input.GetMouseButton(0))
        {
            Break();
        }
        if (Input.GetMouseButton(1))
        {
            Place();
        }
        float my = Input.GetAxis("Mouse X") * 10.0f;
        float mx = Input.GetAxis("Mouse Y") * 10.0f;
        rx += mx;
        ry += my;
        cam.transform.rotation = Quaternion.Euler(new Vector3(-rx, ry, 0));
        if (Input.GetKeyDown(KeyCode.W))
        {
            forward = true;
            if (run)
            {
                running = true;
            }
        }
        if (Input.GetKeyUp(KeyCode.W))
        {
            forward = false;
            running = false;
            run = true;
        }
        if (run)
        {
            runTimer += Time.deltaTime;
            if (runTimer >= 0.25f)
            {
                run = false;
                runTimer = 0;
            }
        }
        if (Input.GetKeyUp(KeyCode.W))
        {
            forward = false;
        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            left = true;
        }
        if (Input.GetKeyUp(KeyCode.A))
        {
            left = false;
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            back = true;
        }
        if (Input.GetKeyUp(KeyCode.S))
        {
            back = false;
        }
        if (Input.GetKeyDown(KeyCode.D))
        {
            right = true;
        }
        if (Input.GetKeyUp(KeyCode.D))
        {
            right = false;
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            up = true;
        }
        if (Input.GetKeyUp(KeyCode.Space))
        {
            up = false;
        }
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            down = true;
        }
        if (Input.GetKeyUp(KeyCode.LeftShift))
        {
            down = false;
        }
        if (Input.GetKeyDown(KeyCode.F))
        {
            fly = fly ? false : true;
        }
        if (Input.GetKey(KeyCode.N))
        {
            speed++;
        }
        else if (speed > 1 && Input.GetKey(KeyCode.M))
        {
            speed--;
        }
        if (Size < 15 && Input.GetKeyDown(KeyCode.J))
        {
            Size++;
        }
        else if (Size > 1 && Input.GetKeyDown(KeyCode.K))
        {
            Size--;
        }
        pos = transform.position;
    }
    void FixedUpdate()
    {
        transform.rotation = Quaternion.Euler(new Vector3(0, ry, 0));
        Vector3 vel = new Vector3();
        float resistance = 0;
        if (fly)
        {
            Quaternion q = Quaternion.Euler(new Vector3(-rx, ry, 0));
            Vector3 ver = new Vector3(0, 0, 0);
            if (forward)
            {
                ver += new Vector3(0, 0, 1);
            }
            if (back)
            {
                ver += new Vector3(0, 0, -1);
            }
            if (left)
            {
                ver += new Vector3(-1, 0, 0);
            }
            if (right)
            {
                ver += new Vector3(1, 0, 0);
            }
            if (up)
            {
                ver += new Vector3(0, 1, 0);
            }
            if (down)
            {
                ver += new Vector3(0, -1, 0);
            }
            ver = Vector3.Normalize(ver);
            ver = q * ver;
            vel += ver * speed * speed * (running ? 2 : 1); ;
            resistance = 0.375f;
        }
        else if (onGround)
        {
            Quaternion q = Quaternion.Euler(new Vector3(-rx, ry, 0));
            Vector3 ver = new Vector3(0, 0, 0);
            if (forward)
            {
                ver += new Vector3(0, 0, 1);
            }
            if (back)
            {
                ver += new Vector3(0, 0, -1);
            }
            if (left)
            {
                ver += new Vector3(-1, 0, 0);
            }
            if (right)
            {
                ver += new Vector3(1, 0, 0);
            }
            ver.Normalize();
            ver = q * ver;
            vel += ver * (running ? 20 : 10);
            if (up)
            {
                vel.y += 10;
            }
            resistance = 0.5f;
        }
        else
        {
            resistance = 0.0005f;
        }
        rb.velocity += vel;
        rb.velocity -= rb.velocity * resistance;
        onGround = false;
        Time.fixedDeltaTime = 1.0f / 50.0f;// /(1+(rb.velocity.magnitude/50.0f*8));
    }

    void OnCollisionEnter(Collision c)
    {
        onGround = true;
    }

    void OnCollisionStay(Collision c)
    {
        onGround = true;
    }

    void OnCollisionExit(Collision c)
    {
        onGround = false;
    }

}
