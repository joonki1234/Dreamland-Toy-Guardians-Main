#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using DreamGuardians;
using Fusion;
[InitializeOnLoad]
public static class BossBattleTemporaryQA
{
 const string Root="Library/BossFaceNetworkQA/", Key="BossFaceNetworkQA.Active";
 static FinalBossDirector director; static FinalBossFaceController face; static EnemyHealth health;
 static int lastSnapshot = -1; static double started; static float fighting=-1, prepAt; static int damageStep,shot; static string last=""; static bool invoked;
 static BossBattleTemporaryQA(){EditorApplication.delayCall+=Start;EditorApplication.playModeStateChanged+=Changed;}
 static void Log(string s){File.AppendAllText(Root+"trace.txt",Time.time.ToString("F3")+" "+s+Environment.NewLine);}
 static void Start(){if(!File.Exists(Root+"request"))return;File.Delete(Root+"request");
  if(EditorApplication.isPlayingOrWillChangePlaymode){Log("BLOCKED: already playing");return;}
  for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty){Log("BLOCKED: unsaved scene");return;}
  SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 static void Changed(PlayModeStateChange s){if(!SessionState.GetBool(Key,false))return;
  if(s==PlayModeStateChange.EnteredPlayMode){started=EditorApplication.timeSinceStartup;fighting=-1;damageStep=0;invoked=false;EditorApplication.update+=Tick;Log("Entered actual scene Play Mode: "+SceneManager.GetActiveScene().path);}
  if(s==PlayModeStateChange.ExitingPlayMode){EditorApplication.update-=Tick;Log("Exiting Play Mode");}
  if(s==PlayModeStateChange.EnteredEditMode)SessionState.SetBool(Key,false);}
 static object Get(object o,string n)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
 static void Damage(float amount){Log("Inject TakeDamage amount="+amount);health.TakeDamage(new DamageInfo(amount,"FACE_QA",PlayerRole.None,++shot,health.transform.position,false));}
 static void Tick(){if(!Application.isPlaying)return;try{
  double elapsed=EditorApplication.timeSinceStartup-started;if(elapsed>110){Finish("TIMEOUT");return;}if(elapsed<3)return;
  if(!invoked){invoked=true;director=UnityEngine.Object.FindAnyObjectByType<FinalBossDirector>();var flow=UnityEngine.Object.FindAnyObjectByType<DreamlandGameFlowController>();
   Log("XR device active="+XRSettings.isDeviceActive+" loaded="+XRSettings.loadedDeviceName);
   var devices=new List<InputDevice>();InputDevices.GetDevices(devices);foreach(var d in devices)Log("XR device="+d.name);
   foreach(var r in NetworkRunner.Instances)Log("Runner running="+r.IsRunning+" mode="+r.GameMode);
   if(flow==null||director==null){Finish("BLOCKED: scene has no boss flow");return;}
   Log("Invoke existing GameFlow TestStartBossBattle");typeof(DreamlandGameFlowController).GetMethod("TestStartBossBattle",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(flow,null);}
  if(director==null)return;
  var transport=Get(director,"enemySpawner") as DreamEnemySpawner;
  if(transport!=null && transport.IsBossFaceNetworkReady && transport.BossFaceState.Revision!=lastSnapshot){var snap=transport.BossFaceState;lastSnapshot=snap.Revision;Log("NETWORK authority="+transport.IsBossFaceAuthority+" flags="+transport.Object.Flags+" state="+snap.Expression+" visible="+snap.Visible+" revision="+snap.Revision+" tickTime="+snap.StartedAt); }
  var boss=Get(director,"bossObject") as GameObject;
  if(boss!=null){face=boss.GetComponent<FinalBossFaceController>();health=boss.GetComponent<EnemyHealth>();}
  if(face==null||health==null)return;
  var spawned=Get(director,"bossSpawnedEnemies") as List<EnemyHealth>;
  string state=director.CurrentState+" face="+face.CurrentExpression+" hp="+health.CurrentHealth.ToString("F1")+" spawned="+spawned.Count;
  if(state!=last){Log(state);if(face.CurrentExpression==FinalBossFaceController.Expression.SummonPrepare)prepAt=Time.time;last=state;
   var renderer=Get(face,"faceRenderer") as Renderer;if(renderer!=null)Log("Face renderer enabled="+renderer.enabled+" position="+renderer.transform.position+" scale="+renderer.transform.lossyScale);
  }
  if(director.CurrentState!=FinalBossDirector.FinalBossState.Fighting)return;
  if(fighting<0){fighting=Time.time;Log("Boss NetworkObject="+(boss.GetComponent<NetworkObject>()!=null));}
  float t=Time.time-fighting;
  if(damageStep==0&&face.CurrentExpression==FinalBossFaceController.Expression.SummonPrepare&&Time.time-prepAt>0.1f){Damage(1);damageStep=1;}
  if(damageStep==1&&t>4&&face.CurrentExpression!=FinalBossFaceController.Expression.Hit){Damage(health.CurrentHealth-health.MaxHealth*0.5f);damageStep=2;}
  if(damageStep==2&&t>15){Damage(health.CurrentHealth-health.MaxHealth*0.2f);damageStep=3;}
  if(t>27)Finish("DONE: actual boss flow observed; see trace for unavailable network/XR and spawn success.");
 }catch(Exception e){Finish("ERROR: "+e);}}
 static void Finish(string s){Log(s);File.WriteAllText(Root+"status.txt",s);EditorApplication.update-=Tick;EditorApplication.ExitPlaymode();}
}
#endif
