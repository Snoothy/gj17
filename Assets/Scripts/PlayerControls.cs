﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerControls : MonoBehaviour
{
    public static event Action<Vector3, int, Color, float> OnStomp;

    public int PlayerId = 0;
    public Rigidbody rbody;
    public Vector3 jumpForce, stompForce;
    public float movespeed = 3;
    public float maxspeed = 3;

    private MoveState _stateInner = MoveState.none;
    public MoveState state
    {
        get { return _stateInner; }
        set
        {
            _stateInner = value;
            switch(_stateInner)
            {
                case MoveState.chargingJump:
                case MoveState.chargingStomp: MyFace.sprite = FaceCharging; break;
                case MoveState.jumping: MyFace.sprite = FaceJump; break;
                case MoveState.hit:
                case MoveState.afterStomp: MyFace.sprite = FaceStunned; break;
                case MoveState.prepareStomp:
                case MoveState.stomping: MyFace.sprite = FacePound; break;
                case MoveState.none: MyFace.sprite = FaceNormal; break;
            }
        }
    }
    public bool isGrounded;
    public bool isInTheAir { get { return !isGrounded; } }
    private float lastJumpTimestamp = float.MinValue;
    private float lastStompTimestamp = float.MinValue;
    public float minJumpCharge = 0.1f;
    public float maxJumpCharge = 1;
    public float minStompCharge = 0.1f;
    public float maxStompCharge = 1;
    public float currentJumpForce = 0;
    public float currentStompForce = 0;
    public float stompStunTimeMax = 1.5f;

    public float maxStompRange = 10;
    public float stompDelay = 0.3f;

    private Rewired.Player player;

    public Renderer MyRenderer;
    public SpriteRenderer MyFace;
    public Sprite FaceNormal, FaceStunned, FacePound, FaceCharging, FaceJump;
    public ParticleSystem psystem;

    public Screenshake screenshaker;

    public Color color { get { return MyRenderer.material.color; } }
    public PlayerColor playercolor = PlayerColor.none;

    public void Setup(int playerid, PlayerColor mycolor)
    {
        PlayerId = playerid;
        SetColor(mycolor);
    }

    static int TEMPVAR = 1;
    private void Start()
    {
        screenshaker = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Screenshake>();
        if(playercolor == PlayerColor.none)
        {
            SetColor((PlayerColor)(TEMPVAR++));
        }
        //transform.position += Vector3.right * PlayerId;
    }

    private void SetColor(PlayerColor mycolor)
    {
        playercolor = mycolor;
        switch (playercolor)
        {
            case PlayerColor.blue: MyRenderer.material.color = Color.blue; break;
            case PlayerColor.green: MyRenderer.material.color = Color.green; break;
            case PlayerColor.orange: MyRenderer.material.color = new Color(255, 176, 32); break;
            case PlayerColor.pink: MyRenderer.material.color = new Color(255, 170, 207); break;
            case PlayerColor.magenta: MyRenderer.material.color = Color.magenta; break;
            case PlayerColor.red: MyRenderer.material.color = Color.red; break;
            case PlayerColor.teal: MyRenderer.material.color = new Color(2, 250, 255); break;
            case PlayerColor.yellow: MyRenderer.material.color = Color.yellow; break;
        }
    }

    void Update()
    {
        player = Rewired.ReInput.players.GetPlayer("Player" + PlayerId); // get the player by id

        if (state == MoveState.afterStomp)
        {
            float minS = Mathf.Abs(minStompCharge * stompForce.y);
            float maxS = Mathf.Abs(maxStompCharge * stompForce.y);
            if (Time.time - lastStompTimestamp < stompStunTimeMax * ((Mathf.Abs(currentStompForce) - minS) / (maxS - minS)))
                return;
            else
                state = MoveState.none;
        }

        //cannot charge or move while we are resolving the hit we received
        if (state == MoveState.hit)
            return; 

        if(state == MoveState.prepareStomp)
        {
            rbody.velocity = Vector3.zero;
        }

        if (player.GetButtonDown("Jump"))
        {
            if (isGrounded)
                //ChargeJump();
                DoJump();
            else
                DoStomp();
            //else
            //   ChargeStomp();
        }

        /*if (player.GetButtonUp("Jump"))
        {
            if (isGrounded)
                DoJump();
            //else
            //    DoStomp();
        }*/

        //cannot move while charging stuff
        if (/*state == MoveState.chargingJump ||*/ state == MoveState.chargingStomp)
            return;

        /*if (player.GetButtonDown("Stomp"))
        {
            DoStomp();
        }*/

        DoInput(player.GetAxis("Move Horizontal"), player.GetAxis("Move Vertically"), state == MoveState.chargingJump);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.contacts[0].point.y < transform.position.y && collision.collider.tag == "Ground")
        {
            if(state == MoveState.stomping)
            {
                PerformStomp(collision.contacts[0].point);
                state = MoveState.afterStomp;
            }
            else
            {
                state = MoveState.none;
            }
            isGrounded = true;
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!isGrounded && collision.contacts[0].point.y < transform.position.y && collision.collider.tag == "Ground" && Time.time - lastJumpTimestamp > 0.25f)
        {
            isGrounded = true;
            if(state != MoveState.afterStomp)
                state = MoveState.none;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        //Start herer
        Stomp stomper = other.transform.parent.GetComponent<Stomp>();
        if (other.tag == "Stomp" && isGrounded && stomper != null && stomper.color != color)
        {
            PerformStompHit((transform.position - other.transform.position).normalized + Vector3.up * 0.3f, stomper.force);
        }
    }

    /*public void ChargeJump()
    {
        if(isGrounded && state != MoveState.chargingJump && state != MoveState.jumping)
        {
            state = MoveState.chargingJump;
            lastJumpTimestamp = Time.time;
        }
    }*/

    public void DoJump()
    {
        if (isGrounded)// && state == MoveState.chargingJump)
        {
            //currentJumpForce = jumpForce.y * Mathf.Clamp(Time.time - lastJumpTimestamp, minJumpCharge, maxJumpCharge);
            currentJumpForce = jumpForce.y * maxJumpCharge;
            rbody.AddForce(currentJumpForce * Vector3.up, ForceMode.VelocityChange);
            isGrounded = false;
            state = MoveState.jumping;
            lastJumpTimestamp = Time.time;
        }
    }

    /*public void ChargeStomp()
    {
        if(!isGrounded && state != MoveState.chargingStomp && state != MoveState.stomping)
        {
            state = MoveState.chargingStomp;
            lastStompTimestamp = Time.time;
        }
    }*/

    public void DoStomp()
    {
        if (!isGrounded)// && state == MoveState.chargingStomp)
        {
            RaycastHit info;
            if(Physics.Raycast(transform.position, Vector3.down, out info, 50, 1<<LayerMask.NameToLayer("Ground")))
            {
                float range = Mathf.Abs(transform.position.y - info.point.y - 0.5f);
                //currentStompForce = stompForce.y * Mathf.Clamp(Time.time - lastStompTimestamp, minStompCharge, maxStompCharge);
                currentStompForce = range / maxStompRange * stompForce.y;
                state = MoveState.prepareStomp;
                StartCoroutine(StompAfterDelay());
            }
        }
    }

    IEnumerator StompAfterDelay()
    {
        float timer = stompDelay;
        Transform t = transform;
        Vector3 tpos = t.position;
        Vector3 tposAdd = Vector3.up * 0.25f;
        while(timer > 0)
        {
            timer -= Time.deltaTime;
            t.position = tpos + (1f - timer / stompDelay) * tposAdd;
            yield return null;
        }

        rbody.AddForce(currentStompForce * Vector3.up, ForceMode.VelocityChange);
        state = MoveState.stomping;
    }

    Vector3 tempInputV;
    public void DoInput(float x, float y, bool ischargingjump)
    {
        if(x != 0 || y != 0)
        {
            rbody.AddForce(new Vector3(x, 0, y) * movespeed * (isInTheAir ? 0.25f : 1f) * (ischargingjump ? 0.8f : 1f), ForceMode.VelocityChange);
            tempInputV = rbody.velocity;
            tempInputV.y = 0;
            if (tempInputV.magnitude > maxspeed)
            {
                tempInputV = tempInputV.normalized * maxspeed;
                tempInputV.y = rbody.velocity.y;
                rbody.velocity = tempInputV;
            }
            /*if (!isInTheAir && rbody.velocity.magnitude > maxspeed)
            {
                rbody.velocity = rbody.velocity.normalized * maxspeed;
            }*/
        }
    }

    void PerformStomp(Vector3 position)
    {
        lastStompTimestamp = Time.time;
        state = MoveState.afterStomp;
        screenshaker.Shake(currentStompForce / stompForce.y);
        if (OnStomp != null)
        {
            OnStomp(position, PlayerId, MyRenderer.material.color, currentStompForce/stompForce.y);
            Debug.Log("STOMP TIME: " + currentStompForce / stompForce.y);
        }
        psystem.Emit(20);
    }

    void PerformStompHit(Vector3 dir, float force)
    {
        rbody.AddForce(dir * force, ForceMode.Impulse);
        isGrounded = false;
        state = MoveState.hit;
        lastJumpTimestamp = Time.time;
    }
}

public enum MoveState { none, chargingJump, chargingStomp, stomping, jumping, hit, afterStomp, prepareStomp }
public enum PlayerColor { /*black, white,*/ none, green, red, yellow, blue, orange, magenta, teal, pink }