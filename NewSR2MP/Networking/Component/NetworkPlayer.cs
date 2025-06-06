﻿
using NewSR2MP.Networking.Packet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Il2CppTMPro;
using UnityEngine;

namespace NewSR2MP.Networking.Component
{
    [RegisterTypeInIl2Cpp(false)]
    public class NetworkPlayer : MonoBehaviour
    {
        void Awake()
        {
            if (transform.GetComponents<NetworkPlayer>().Length > 1)
            {
                Destroy(this);
            }

            try
            {
            }
            catch { }
        }

        public TextMesh usernamePanel;
        
        public int id;
        float transformTimer = 0.1f;

        public void Update()
        {
            transformTimer -= Time.unscaledDeltaTime;
            if (transformTimer < 0)
            {
                transformTimer = 0.1f;
                
                var anim = GetComponent<Animator>();
                
                var packet = new PlayerUpdateMessage()
                {
                    id = id,
                    scene = (byte)sceneGroupsReverse[systemContext.SceneLoader.CurrentSceneGroup.name],
                    pos = transform.position,
                    rot = transform.rotation,
                    horizontalMovement = anim.GetFloat("HorizontalMovement"),
                    forwardMovement = anim.GetFloat("ForwardMovement"),
                    yaw = anim.GetFloat("Yaw"),
                    airborneState = anim.GetInteger("AirborneState"),
                    moving = anim.GetBool("Moving"),
                    horizontalSpeed = anim.GetFloat("HorizontalSpeed"),
                    forwardSpeed = anim.GetFloat("ForwardSpeed"),
                    sprinting = anim.GetBool("Sprinting"),
                };
                MultiplayerManager.NetworkSend(packet);

            }
        }
    }
}
