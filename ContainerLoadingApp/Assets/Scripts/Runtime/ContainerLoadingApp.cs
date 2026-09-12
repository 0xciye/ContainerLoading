using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed partial class ContainerLoadingApp : MonoBehaviour
{
    enum ScreenMode { Home, Container, Cargo, Detail, Editor, Plans, Settings }
    enum PlacementInputMode { Touch, Controls }
    enum CargoFilter { All, Remaining, Complete }
    LoadingPlan plan; ScreenMode screen; Camera cam; Transform root;
    readonly List<GameObject> visuals=new(); readonly Dictionary<GameObject,string> visualIds=new(); readonly Stack<string> undo=new(); readonly Stack<string> redo=new();
    Vector2 scroll,pointerStart,lastPointer; bool pointerMoved,pointerOverUi,orbiting,editingContainer,formDirty,sortNewest=true,placementMode,busy,suppressEditorPointer,cargoFormVisible; GameObject modalShade; Coroutine autosaveRoutine; float pinch,uiScale=1; int selectedType,rotation,layer,colorIndex; Vector3Int previewPosition; PlacementInputMode placementInputMode=PlacementInputMode.Touch; CargoFilter cargoFilter; string selectedId,editingTypeId,lastPdf,lastDataExport,status="Sẵn sàng",search="",cargoSearch="",deletePlanPath,editingOriginalPath,pendingAutoPlacementBackup; CargoType deleteCargoTarget;
    string containerCode="CONT001",containerName="Container",containerLength="10",containerWidth="3",containerHeight="6";
    string orderReference="",customerName="",containerNumber="",sealNumber="",destination="",loadingDate="",orderNotes="";
    string cargoCode="A",cargoName="Hàng A",cargoLength="1",cargoWidth="1",cargoHeight="1",cargoQuantity="1",cargoWeight="0"; bool cargoAllowRotation=true,cargoStackable=true; Color cargoColor=new(.12f,.55f,.95f);
    readonly Color[] palette={new(.12f,.55f,.95f),new(.95f,.7f,.1f),new(.9f,.2f,.2f),new(.2f,.75f,.32f),new(.65f,.3f,.9f),new(.1f,.75f,.75f),new(.95f,.4f,.72f),new(.55f,.55f,.55f)};
    GUIStyle title,heading,button,primaryButton,dangerButton,label,muted,card,input,badge; Texture2D brandIcon,whiteTex,surfaceTex,accentTex,dangerTex,mutedTex; float ViewWidth=>Screen.width/uiScale; float ViewHeight=>Screen.height/uiScale; float PanelWidth=>Mathf.Min(340f,ViewWidth*.48f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Bootstrap(){if(!FindAnyObjectByType<ContainerLoadingApp>())new GameObject(nameof(ContainerLoadingApp)).AddComponent<ContainerLoadingApp>();}
    void Awake(){plan=PlanPersistence.Load()??new LoadingPlan();EnsurePlanDefaults();screen=ScreenMode.Home;SetupScene();Rebuild();SetupMobileUI();SetupUpdates();}
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            if(modalShade){Destroy(modalShade);modalShade=null;return;}
            if(screen==ScreenMode.Home)Application.Quit();
            else RequestBackNavigation();
        }
        if(screen==ScreenMode.Editor){if(suppressEditorPointer){if(Input.touchCount==0&&!Input.GetMouseButton(0))suppressEditorPointer=false;return;}HandlePointer();}
    }

    void EnsurePlanDefaults(){PlanValidation.Normalize(plan);foreach(var type in plan.cargoTypes)if(type.color.a<=0)type.color=DefaultColor(type.id);}
    Color DefaultColor(string key){var hash=17;foreach(var c in key??"")hash=hash*31+c;return palette[Mathf.Abs(hash)%palette.Length];}
    void SetupScene(){cam=Camera.main??new GameObject("Main Camera").AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.055f,.075f,.1f);cam.fieldOfView=55;root=new GameObject("ContainerVisuals").transform;var light=new GameObject("Key Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.transform.rotation=Quaternion.Euler(50,-35,0);ResetCamera();}

    void HandlePointer(){if(Input.touchCount>=2){if(pointerOverUi)return;var d=Vector2.Distance(Input.touches[0].position,Input.touches[1].position);if(pinch>0)Zoom((d-pinch)*.015f);pinch=d;return;}pinch=0;if(Input.touchCount==1){var t=Input.touches[0];if(t.phase==TouchPhase.Began){pointerOverUi=EventSystem.current&&EventSystem.current.IsPointerOverGameObject(t.fingerId);if(!pointerOverUi)BeginPointer(t.position);}else if(pointerOverUi){if(t.phase==TouchPhase.Ended||t.phase==TouchPhase.Canceled)pointerOverUi=false;}else if(t.phase==TouchPhase.Moved)MovePointer(t.position);else if(t.phase==TouchPhase.Ended&&!pointerMoved)TryTap(t.position);return;}if(Input.GetMouseButtonDown(0)){pointerOverUi=EventSystem.current&&EventSystem.current.IsPointerOverGameObject();if(!pointerOverUi)BeginPointer(Input.mousePosition);}if(pointerOverUi){if(Input.GetMouseButtonUp(0))pointerOverUi=false;return;}if(Input.GetMouseButton(0))MovePointer(Input.mousePosition);if(Input.GetMouseButtonUp(0)&&!pointerMoved)TryTap(Input.mousePosition);if(Input.GetMouseButtonDown(1)){orbiting=true;lastPointer=Input.mousePosition;}if(Input.GetMouseButtonUp(1))orbiting=false;if(orbiting)Orbit((Vector2)Input.mousePosition-lastPointer);lastPointer=Input.mousePosition;Zoom(Input.mouseScrollDelta.y);}
    void BeginPointer(Vector2 p){pointerStart=lastPointer=p;pointerMoved=false;}
    void MovePointer(Vector2 p){if(p.x<PanelWidth*uiScale)return;var delta=p-lastPointer;if(Vector2.Distance(p,pointerStart)>12)pointerMoved=true;if(pointerMoved)Orbit(delta);lastPointer=p;}
    void Orbit(Vector2 delta){cam.transform.RotateAround(Center(),Vector3.up,delta.x*.18f);cam.transform.RotateAround(Center(),cam.transform.right,-delta.y*.12f);cam.transform.LookAt(Center());}
    void Zoom(float amount){if(Mathf.Abs(amount)<.001f)return;var distance=Vector3.Distance(cam.transform.position,Center());var step=Mathf.Clamp(amount,-2,2);if(distance>2||step<0)cam.transform.position+=cam.transform.forward*step;}

    void TryTap(Vector2 point){if(placementInputMode!=PlacementInputMode.Touch||point.x<PanelWidth*uiScale)return;var ray=cam.ScreenPointToRay(point);if(Physics.Raycast(ray,out var hit)&&visualIds.TryGetValue(hit.collider.gameObject,out var id)){selectedId=id;var box=FindPlaced(id);status=box==null?"Không tìm thấy thùng.":$"Đã chọn thùng · Cột {box.position.x+1} · Hàng {box.position.y+1} · Tầng {box.position.z+1}";Rebuild();return;}if(plan.cargoTypes.Count==0){status="Hãy tạo ít nhất một loại hàng.";return;}selectedType=Mathf.Clamp(selectedType,0,plan.cargoTypes.Count-1);var plane=new Plane(Vector3.up,new Vector3(0,layer,0));if(!plane.Raycast(ray,out var enter))return;var p=ray.GetPoint(enter);SetPreview(new Vector3Int(Mathf.FloorToInt(p.x),Mathf.FloorToInt(p.z),layer));}
    void SetPreview(Vector3Int position,bool autoStack=true){var type=plan.cargoTypes[Mathf.Clamp(selectedType,0,plan.cargoTypes.Count-1)];var size=GridPlacement.RotatedSize(new Vector3Int(type.length,type.width,type.height),rotation);previewPosition=new Vector3Int(Mathf.Clamp(position.x,0,Mathf.Max(0,plan.container.length-size.x)),Mathf.Clamp(position.y,0,Mathf.Max(0,plan.container.width-size.y)),Mathf.Clamp(position.z,0,Mathf.Max(0,plan.container.height-size.z)));if(autoStack)previewPosition.z=GridPlacement.LowestSupportedLayer(plan,type,previewPosition,rotation,selectedId);layer=previewPosition.z;placementMode=true;var occupiedLayers=size.z>1?$"{previewPosition.z+1}–{previewPosition.z+size.z}":$"{previewPosition.z+1}";if(!GridPlacement.TryPlace(plan,type,previewPosition,rotation,selectedId,out var error))status="✕ "+error;else status=$"✓ Có thể đặt · Cột {previewPosition.x+1} · Hàng {previewPosition.y+1} · Tầng {occupiedLayers}";Rebuild(true);}
    void Place(CargoType type,Vector3Int position,int boxRotation){if(!GridPlacement.TryPlace(plan,type,position,boxRotation,selectedId,out var error)){status="✕ "+error;Rebuild(true);return;}var existing=FindPlaced(selectedId);Snapshot();if(existing!=null){existing.position=position;existing.rotation=boxRotation;existing.size=GridPlacement.RotatedSize(new Vector3Int(type.length,type.width,type.height),boxRotation);status=$"Đã di chuyển {type.code}.";placementMode=false;}else{plan.placedCargo.Add(new PlacedCargo{id=Guid.NewGuid().ToString("N"),cargoTypeId=type.id,position=position,size=GridPlacement.RotatedSize(new Vector3Int(type.length,type.width,type.height),boxRotation),rotation=boxRotation});var placed=plan.placedCargo.Count(x=>x.cargoTypeId==type.id);if(PlanIntelligence.TryFindNextPlacement(plan,type,out var next,out var nextRotation)){previewPosition=next;rotation=nextRotation;layer=next.z;placementMode=true;status=$"Đã đặt {placed}/{type.quantity} kiện {type.code} · vị trí tiếp theo đã sẵn sàng.";}else{placementMode=false;status=placed>=type.quantity?$"Đã xếp đủ {type.quantity} kiện {type.code}.":$"Đã đặt {placed}/{type.quantity}; không còn vị trí phù hợp.";}}selectedId=null;QueueAutosave();Rebuild(true);}
    void ConfirmPreview(){if(!placementMode||plan.cargoTypes.Count==0){status="Chọn hàng rồi chạm vào một ô để xem trước.";Rebuild();return;}var type=plan.cargoTypes[Mathf.Clamp(selectedType,0,plan.cargoTypes.Count-1)];Place(type,previewPosition,rotation);}
    void CancelPreview(){placementMode=false;selectedId=null;status="Đã hủy xem trước.";Rebuild();}
    void BeginPlacement(int index){selectedType=Mathf.Clamp(index,0,plan.cargoTypes.Count-1);selectedId=null;rotation=0;var type=plan.cargoTypes[selectedType];previewPosition=new Vector3Int(Mathf.Max(0,(plan.container.length-type.length)/2),Mathf.Max(0,(plan.container.width-type.width)/2),Mathf.Clamp(layer,0,Mathf.Max(0,plan.container.height-type.height)));SetPreview(previewPosition);}
    void AdjustPreview(int axis,int delta){var p=previewPosition;p[axis]+=delta;SetPreview(p,axis!=2);}
    void SetPreviewAxis(int axis,string value){if(!int.TryParse(value,out var parsed))return;var p=previewPosition;p[axis]=Mathf.Max(0,parsed-1);SetPreview(p);}
    void UpdatePreviewAxisInput(int axis,string value){if(!int.TryParse(value,out var parsed))return;var p=previewPosition;p[axis]=Mathf.Max(0,parsed-1);previewPosition=p;}
    void DeleteSelected(){var box=FindPlaced(selectedId);if(box==null){status="Chạm vào một thùng trước.";return;}Snapshot();plan.placedCargo.Remove(box);selectedId=null;status="Đã xóa thùng.";QueueAutosave();Rebuild(true);}
    void MoveSelected(){var box=FindPlaced(selectedId);if(box==null){status="Chạm vào một thùng trước.";return;}var index=plan.cargoTypes.FindIndex(x=>x.id==box.cargoTypeId);if(index<0)return;selectedType=index;rotation=box.rotation;previewPosition=box.position;placementMode=true;status="Đang di chuyển · chỉnh Cột/Hàng/Tầng hoặc chạm ô mới.";Rebuild();}
    void RotateSelected(){if(placementMode){var previewType=plan.cargoTypes[Mathf.Clamp(selectedType,0,plan.cargoTypes.Count-1)];if(!previewType.allowRotation){status="Loại hàng này phải giữ nguyên hướng.";RefreshMobileUI(true);return;}rotation=(rotation+90)%360;SetPreview(previewPosition);return;}var box=FindPlaced(selectedId);if(box==null){rotation=(rotation+90)%360;status="Hướng đặt mới: "+rotation+"°";return;}var type=plan.cargoTypes.Find(x=>x.id==box.cargoTypeId);if(type==null||!type.allowRotation){status="Loại hàng này không cho phép xoay.";return;}var index=plan.placedCargo.IndexOf(box);plan.placedCargo.RemoveAt(index);var next=(box.rotation+90)%360;if(!GridPlacement.TryPlace(plan,type,box.position,next,out var error)){plan.placedCargo.Insert(index,box);status=error;return;}plan.placedCargo.Insert(index,box);Snapshot();box.rotation=next;box.size=GridPlacement.RotatedSize(new Vector3Int(type.length,type.width,type.height),next);status="Đã xoay thùng "+next+"°.";QueueAutosave();Rebuild(true);}
    void Snapshot(){undo.Push(JsonUtility.ToJson(plan));redo.Clear();}
    void Undo(){if(undo.Count==0){status="Không còn thao tác để hoàn tác.";return;}redo.Push(JsonUtility.ToJson(plan));plan=JsonUtility.FromJson<LoadingPlan>(undo.Pop());selectedId=null;status="Đã hoàn tác.";QueueAutosave();Rebuild(true);}
    void Redo(){if(redo.Count==0){status="Không còn thao tác để làm lại.";return;}undo.Push(JsonUtility.ToJson(plan));plan=JsonUtility.FromJson<LoadingPlan>(redo.Pop());selectedId=null;status="Đã làm lại.";QueueAutosave();Rebuild(true);}
    void QueueAutosave(){if(autosaveRoutine!=null)StopCoroutine(autosaveRoutine);autosaveRoutine=StartCoroutine(AutosaveAfterDelay());}
    IEnumerator AutosaveAfterDelay(){yield return new WaitForSecondsRealtime(.8f);autosaveRoutine=null;try{PlanPersistence.Save(plan);}catch(Exception exception){Debug.LogException(exception);status="Không thể tự động lưu. Hãy nhấn Lưu sơ đồ.";if(mobileCanvas)RefreshMobileUI(true);}}
    void SuggestPlacement(){if(plan.cargoTypes.Count==0)return;var type=plan.cargoTypes[Mathf.Clamp(selectedType,0,plan.cargoTypes.Count-1)];selectedId=null;if(!PlanIntelligence.TryFindNextPlacement(plan,type,out var position,out var suggestedRotation)){status=$"Không tìm thấy vị trí phù hợp cho {type.code}.";RefreshMobileUI(true);return;}rotation=suggestedRotation;SetPreview(position,false);status=$"✓ Đã đề xuất vị trí cho {type.code}.";}
    void PreviewAutoFill(){if(plan.cargoTypes.Count==0)return;pendingAutoPlacementBackup=JsonUtility.ToJson(plan);var added=PlanIntelligence.AutoFillRemaining(plan);if(added==0){plan=JsonUtility.FromJson<LoadingPlan>(pendingAutoPlacementBackup);pendingAutoPlacementBackup=null;status="Không còn kiện hoặc vị trí phù hợp để xếp tự động.";RefreshMobileUI(true);return;}placementMode=false;selectedId=null;status=$"Đang xem trước {added} kiện được xếp tự động.";Rebuild(true);ShowAutoFillPreview(added);}
    void AcceptAutoFill(){if(string.IsNullOrEmpty(pendingAutoPlacementBackup))return;undo.Push(pendingAutoPlacementBackup);redo.Clear();pendingAutoPlacementBackup=null;status="Đã chấp nhận phương án xếp tự động. Có thể hoàn tác bằng một bước.";QueueAutosave();Rebuild(true);}
    void CancelAutoFill(){if(string.IsNullOrEmpty(pendingAutoPlacementBackup))return;plan=JsonUtility.FromJson<LoadingPlan>(pendingAutoPlacementBackup);pendingAutoPlacementBackup=null;status="Đã hủy bản xem trước xếp tự động.";Rebuild(true);}
    PlacedCargo FindPlaced(string id)=>string.IsNullOrEmpty(id)?null:plan.placedCargo.Find(x=>x.id==id);

    void Rebuild(bool preserveMobileScroll=false){foreach(var go in visuals)if(go)Destroy(go);visuals.Clear();visualIds.Clear();CreateBoundary();foreach(var box in plan.placedCargo){var type=plan.cargoTypes.Find(x=>x.id==box.cargoTypeId);if(type==null)continue;var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name="Cargo_"+type.code;cube.transform.SetParent(root);cube.transform.position=new Vector3(box.position.x,box.position.z,box.position.y)+new Vector3(box.size.x,box.size.z,box.size.y)*.5f;cube.transform.localScale=new Vector3(box.size.x,box.size.z,box.size.y)*.96f;ApplyCargoMaterial(cube.GetComponent<Renderer>(),type.color);visuals.Add(cube);visualIds[cube]=box.id;}if(placementMode&&plan.cargoTypes.Count>0)CreatePreview();if(mobileCanvas)RefreshMobileUI(preserveMobileScroll);}
    void CreatePreview(){var type=plan.cargoTypes[Mathf.Clamp(selectedType,0,plan.cargoTypes.Count-1)];var size=GridPlacement.RotatedSize(new Vector3Int(type.length,type.width,type.height),rotation);var ghost=GameObject.CreatePrimitive(PrimitiveType.Cube);ghost.name="CargoGhost";ghost.transform.SetParent(root);ghost.transform.position=new Vector3(previewPosition.x,previewPosition.z,previewPosition.y)+new Vector3(size.x,size.z,size.y)*.5f;ghost.transform.localScale=new Vector3(size.x,size.z,size.y)*.98f;var valid=GridPlacement.TryPlace(plan,type,previewPosition,rotation,selectedId,out _);ApplyCargoMaterial(ghost.GetComponent<Renderer>(),new Color(type.color.r,type.color.g,type.color.b,valid?.65f:.35f));visuals.Add(ghost);CreatePreviewCellOutline();}
    void ApplyCargoMaterial(Renderer renderer,Color color){var shader=Shader.Find("Unlit/Color")??Shader.Find("Sprites/Default");renderer.material=new Material(shader){color=color};}
    void CreatePreviewCellOutline(){var y=previewPosition.z+.035f;var a=new Vector3(previewPosition.x,y,previewPosition.y);var b=a+new Vector3(1,0,0);var c=b+new Vector3(0,0,1);var d=a+new Vector3(0,0,1);var material=new Material(Shader.Find("Sprites/Default")){color=Color.white};Line(a,b,.035f,material,"PreviewCell");Line(b,c,.035f,material,"PreviewCell");Line(c,d,.035f,material,"PreviewCell");Line(d,a,.035f,material,"PreviewCell");}
    void SetPlacementInputMode(PlacementInputMode mode){placementInputMode=mode;status=mode==PlacementInputMode.Touch?"Chạm vào ô trên mô hình 3D để chọn vị trí.":"Dùng nút điều hướng để di chuyển preview.";if(mobileCanvas)RefreshMobileUI(true);else Rebuild();}
    void CreateBoundary(){var c=plan.container;var corners=new[]{new Vector3(0,0,0),new Vector3(c.length,0,0),new Vector3(0,c.height,0),new Vector3(c.length,c.height,0),new Vector3(0,0,c.width),new Vector3(c.length,0,c.width),new Vector3(0,c.height,c.width),new Vector3(c.length,c.height,c.width)};int[,] edges={{0,1},{0,2},{0,4},{1,3},{1,5},{2,3},{2,6},{3,7},{4,5},{4,6},{5,7},{6,7}};var material=new Material(Shader.Find("Sprites/Default")){color=new Color(.35f,.75f,1,.9f)};for(var i=0;i<edges.GetLength(0);i++)Line(corners[edges[i,0]],corners[edges[i,1]],.035f,material,"ContainerEdge");for(var x=0;x<=c.length;x++){Line(new Vector3(x,0,0),new Vector3(x,0,c.width),.012f,material,"GridLine");Line(new Vector3(x,layer,0),new Vector3(x,layer,c.width),.018f,material,"LayerGuide");}for(var y=0;y<=c.width;y++){Line(new Vector3(0,0,y),new Vector3(c.length,0,y),.012f,material,"GridLine");Line(new Vector3(0,layer,y),new Vector3(c.length,layer,y),.018f,material,"LayerGuide");}}
    void Line(Vector3 a,Vector3 b,float width,Material material,string name){var go=new GameObject(name);go.transform.SetParent(root);var line=go.AddComponent<LineRenderer>();line.positionCount=2;line.SetPositions(new[]{a,b});line.startWidth=line.endWidth=width;line.material=material;visuals.Add(go);}
    Vector3 Center()=>new(plan.container.length*.5f,plan.container.height*.5f,plan.container.width*.5f);
    void ResetCamera(){var d=Mathf.Max(plan.container.length,plan.container.height,plan.container.width)*1.6f;cam.transform.position=Center()+new Vector3(d*.65f,d*.65f,-d);cam.transform.LookAt(Center());}
    void TopView(){cam.transform.position=Center()+Vector3.up*Mathf.Max(plan.container.length,plan.container.width)*1.8f;cam.transform.rotation=Quaternion.Euler(90,0,0);}
    void FrontView(){cam.transform.position=Center()+Vector3.back*Mathf.Max(plan.container.length,plan.container.height)*1.8f;cam.transform.LookAt(Center());}
    void SideView(){cam.transform.position=Center()+Vector3.right*Mathf.Max(plan.container.width,plan.container.height)*2.2f;cam.transform.LookAt(Center());}
    void FocusSelected(){var box=FindPlaced(selectedId);if(box==null){status="Hãy chọn một kiện hàng trước.";RefreshMobileUI(true);return;}var center=new Vector3(box.position.x+box.size.x*.5f,box.position.z+box.size.z*.5f,box.position.y+box.size.y*.5f);var distance=Mathf.Max(3f,Mathf.Max(box.size.x,Mathf.Max(box.size.y,box.size.z))*3f);cam.transform.position=center+new Vector3(distance*.65f,distance*.55f,-distance);cam.transform.LookAt(center);status="Đã tập trung camera vào kiện đang chọn.";RefreshMobileUI(true);}

    void LegacyOnGUI()
    {
        if(mobileCanvas)return;
        uiScale=Mathf.Max(1f,Mathf.Min(Screen.width/540f,Screen.height/720f));
        var previous=GUI.matrix;
        GUI.matrix=Matrix4x4.Scale(new Vector3(uiScale,uiScale,1));
        BuildStyles();
        GUI.DrawTexture(new Rect(0,0,ViewWidth,ViewHeight),mutedTex);
        if(screen==ScreenMode.Editor) DrawEditor();
        else
        {
            DrawHeader();
            var width=Mathf.Min(700,ViewWidth-28);
            GUILayout.BeginArea(new Rect((ViewWidth-width)*.5f,76,width,ViewHeight-88));
            scroll=GUILayout.BeginScrollView(scroll,false,false);
            if(screen==ScreenMode.Home)DrawHome();
            else if(screen==ScreenMode.Container)DrawContainer();
            else if(screen==ScreenMode.Cargo)DrawCargo();
            else if(screen==ScreenMode.Detail)DrawDetail();
            else if(screen==ScreenMode.Settings)DrawSettings();
            else DrawHome();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        DrawConfirmation();
        GUI.matrix=previous;
    }

    void BuildStyles()
    {
        if(title!=null)return;
        whiteTex=Solid(new Color(1,1,1,1));surfaceTex=Solid(new Color(.965f,.975f,.985f,1));accentTex=Solid(new Color(.035f,.39f,.55f,1));dangerTex=Solid(new Color(.78f,.16f,.19f,1));mutedTex=Solid(new Color(.925f,.945f,.96f,1));
        brandIcon=Resources.Load<Texture2D>("AppIcon");
        title=new GUIStyle(GUI.skin.label){fontSize=27,fontStyle=FontStyle.Bold,wordWrap=true,normal={textColor=new Color(.06f,.1f,.15f)}};
        heading=new GUIStyle(title){fontSize=19};
        label=new GUIStyle(GUI.skin.label){fontSize=15,wordWrap=true,normal={textColor=new Color(.12f,.16f,.21f)},padding=new RectOffset(2,2,2,2)};
        muted=new GUIStyle(label){fontSize=13,normal={textColor=new Color(.38f,.44f,.5f)}};
        button=new GUIStyle(GUI.skin.button){fontSize=15,fixedHeight=44,normal={background=surfaceTex,textColor=new Color(.08f,.16f,.22f)},hover={background=whiteTex,textColor=new Color(.08f,.16f,.22f)},active={background=mutedTex,textColor=new Color(.08f,.16f,.22f)},border=new RectOffset(),margin=new RectOffset(3,3,4,4),padding=new RectOffset(12,12,8,8)};
        primaryButton=new GUIStyle(button){fontStyle=FontStyle.Bold,normal={background=accentTex,textColor=Color.white},hover={background=accentTex,textColor=Color.white},active={background=accentTex,textColor=Color.white}};
        dangerButton=new GUIStyle(button){normal={background=dangerTex,textColor=Color.white},hover={background=dangerTex,textColor=Color.white},active={background=dangerTex,textColor=Color.white}};
        card=new GUIStyle(GUI.skin.box){normal={background=whiteTex},padding=new RectOffset(18,18,16,16),margin=new RectOffset(2,2,7,7)};
        input=new GUIStyle(GUI.skin.textField){fontSize=16,fixedHeight=43,padding=new RectOffset(12,12,8,8),normal={background=whiteTex,textColor=new Color(.08f,.12f,.17f)},focused={background=whiteTex,textColor=new Color(.08f,.12f,.17f)}};
        badge=new GUIStyle(label){fontSize=12,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={background=surfaceTex,textColor=new Color(.035f,.39f,.55f)},fixedHeight=28};
    }

    Texture2D Solid(Color color){var texture=new Texture2D(1,1){hideFlags=HideFlags.HideAndDontSave};texture.SetPixel(0,0,color);texture.Apply();return texture;}
    void DrawHeader()
    {
        GUI.DrawTexture(new Rect(0,0,ViewWidth,64),whiteTex);
        if(brandIcon)GUI.DrawTexture(new Rect(15,8,48,48),brandIcon,ScaleMode.ScaleToFit);
        GUI.Label(new Rect(72,8,ViewWidth-180,28),"CONTAINER LOADING",heading);
        GUI.Label(new Rect(73,35,ViewWidth-180,20),"Quản lý & trực quan hóa xếp hàng",muted);
        if(GUI.Button(new Rect(ViewWidth-101,10,88,43),screen==ScreenMode.Home?"Cài đặt":"Trang chủ",button)){if(screen==ScreenMode.Home){screen=ScreenMode.Settings;scroll=Vector2.zero;}else OpenHome();}
    }
    void Section(string text){GUILayout.Space(8);GUILayout.Label(text,heading);GUILayout.Space(3);}
    void Field(string text,ref string value){GUILayout.Label(text,label);value=GUILayout.TextField(value,input);GUILayout.Space(5);}
    void Notice(){if(!string.IsNullOrWhiteSpace(status)){GUILayout.BeginVertical(card);GUILayout.Label(status,label);GUILayout.EndVertical();}}
    void OpenHome(){screen=ScreenMode.Home;scroll=Vector2.zero;deletePlanPath=null;deleteCargoTarget=null;if(mobileCanvas)RefreshMobileUI();}
    void OpenDetail(){if(!TrySaveCurrent())return;screen=ScreenMode.Detail;scroll=Vector2.zero;selectedId=null;placementMode=false;suppressEditorPointer=false;if(mobileCanvas)RefreshMobileUI();}

    bool TrySaveCurrent(string previousPath=null)
    {
        if(busy)return false;
        busy=true;
        try{PlanPersistence.Save(plan,previousPath);status="Đã lưu dữ liệu an toàn.";return true;}
        catch(Exception exception){Debug.LogException(exception);status="Không thể lưu dữ liệu: "+exception.Message;return false;}
        finally{busy=false;}
    }

    void ExportPlanData()
    {
        if(busy)return;
        busy=true;
        try{lastDataExport=PlanPersistence.ExportJson(plan);status=PdfPlatform.ShareJson(lastDataExport)?"Đã tạo bản sao dữ liệu JSON.":"Đã tạo JSON nhưng không mở được trình chia sẻ.";}
        catch(Exception exception){Debug.LogException(exception);status="Không thể xuất dữ liệu. Hãy kiểm tra dung lượng lưu trữ.";}
        finally{busy=false;if(mobileCanvas)RefreshMobileUI();}
    }

    void ExportFullBackup()
    {
        try { lastDataExport=PlanPersistence.ExportBackup();status=PdfPlatform.ShareJson(lastDataExport)?"Đã tạo bản sao lưu toàn bộ.":"Đã tạo bản sao lưu nhưng không mở được trình chia sẻ."; }
        catch(Exception exception){Debug.LogException(exception);status="Không thể tạo bản sao lưu.";}
        if(mobileCanvas)RefreshMobileUI(true);
    }

    void BeginImportBackup(){if(!DataTransferPlatform.PickJson(gameObject.name,nameof(OnBackupPicked),out var error)){status=error;RefreshMobileUI(true);}}
    public void OnBackupPicked(string path)
    {
        if(string.IsNullOrWhiteSpace(path))return;
        if(path.StartsWith("ERROR:")){status="Không thể đọc tệp: "+path.Substring(6);RefreshMobileUI(true);return;}
        if(!PlanPersistence.TryReadBackup(path,out var backup,out var error)){status="Bản sao lưu không hợp lệ: "+error;RefreshMobileUI(true);return;}
        Confirm("Khôi phục toàn bộ dữ liệu",$"Đã kiểm tra {backup.plans.Count} phương án hợp lệ. Dữ liệu hiện tại không bị ghi đè; phương án trùng tên sẽ được đổi tên.",()=>{try{var count=PlanPersistence.RestoreBackup(backup);status=$"Đã khôi phục {count} phương án.";OpenHome();}catch(Exception exception){Debug.LogException(exception);status="Không thể khôi phục dữ liệu.";RefreshMobileUI(true);}},"Khôi phục","Hủy");
    }

    void ExportCargoCsv(){try{lastDataExport=PlanPersistence.ExportCargoCsv(plan);status=PdfPlatform.ShareCsv(lastDataExport)?"Đã xuất danh mục hàng CSV.":"Đã tạo CSV nhưng không mở được trình chia sẻ.";}catch(Exception exception){Debug.LogException(exception);status="Không thể xuất CSV.";}RefreshMobileUI(true);}
    void BeginImportCargoCsv(){if(!DataTransferPlatform.PickCsv(gameObject.name,nameof(OnCargoCsvPicked),out var error)){status=error;RefreshMobileUI(true);}}
    public void OnCargoCsvPicked(string path)
    {
        if(string.IsNullOrWhiteSpace(path))return;
        if(path.StartsWith("ERROR:")){status="Không thể đọc CSV: "+path.Substring(6);RefreshMobileUI(true);return;}
        if(!PlanPersistence.TryReadCargoCsv(path,out var types,out var error)){status="CSV không hợp lệ: "+error;RefreshMobileUI(true);return;}
        Confirm("Nhập danh mục CSV",$"Đã kiểm tra {types.Count} loại hàng. Chỉ nhập khi toàn bộ dữ liệu hợp lệ.",()=>{var duplicate=types.FirstOrDefault(x=>plan.cargoTypes.Any(y=>string.Equals(x.code,y.code,StringComparison.OrdinalIgnoreCase)));if(duplicate!=null){status="Không thể nhập vì mã hàng đã tồn tại: "+duplicate.code;RefreshMobileUI(true);return;}Snapshot();plan.cargoTypes.AddRange(types);if(!TrySaveCurrent())Undo();else{status=$"Đã nhập {types.Count} loại hàng từ CSV.";Rebuild();}},"Nhập CSV","Hủy");
    }

    void ExportPdf()
    {
        if(busy)return;
        var succeeded=false;busy=true;
        try{lastPdf=PdfExportSystem.Export(plan);status="Đã tạo báo cáo PDF.";succeeded=true;}
        catch(Exception exception){Debug.LogException(exception);status="Không thể tạo PDF. Hãy kiểm tra dữ liệu và dung lượng lưu trữ.";}
        finally{busy=false;if(mobileCanvas){RefreshMobileUI();if(succeeded)ShowPdfReadyDialog();}}
    }

    void OpenPdf(){status=!string.IsNullOrEmpty(lastPdf)&&PdfPlatform.Open(lastPdf)?"Đã mở báo cáo PDF.":"Không tìm thấy PDF hoặc thiết bị không có ứng dụng mở PDF.";if(mobileCanvas)RefreshMobileUI();}
    void SharePdf(){status=!string.IsNullOrEmpty(lastPdf)&&PdfPlatform.Share(lastPdf)?"Đã mở trình chia sẻ PDF.":"Không thể chia sẻ PDF trên thiết bị này.";if(mobileCanvas)RefreshMobileUI();}

    void BeginImportPlan()
    {
        if(busy)return;
        if(!DataTransferPlatform.PickJson(gameObject.name,nameof(OnJsonImportPicked),out var error)){status=error;if(mobileCanvas)RefreshMobileUI();}
    }

    public void OnJsonImportPicked(string path)
    {
        if(string.IsNullOrEmpty(path)){status="Đã hủy nhập dữ liệu.";if(mobileCanvas)RefreshMobileUI();return;}
        if(path.StartsWith("ERROR:",StringComparison.Ordinal)){status="Không thể đọc tệp đã chọn: "+path.Substring(6);if(mobileCanvas)RefreshMobileUI();return;}
        if(!PlanPersistence.TryReadExternal(path,out var preview,out var error)){status="Không thể nhập dữ liệu: "+error;if(mobileCanvas)RefreshMobileUI();return;}
        var message=$"Tên: {preview.name}\nContainer: {preview.container.name} · {preview.container.length}×{preview.container.width}×{preview.container.height}\nLoại hàng: {preview.cargoTypes.Count}\nKiện đã xếp: {preview.placedCargo.Count}\nCập nhật: {preview.updatedAt}";
        Confirm("Xem trước dữ liệu nhập",message,()=>ImportValidatedPlan(path),"Nhập dữ liệu","Hủy");
    }

    void ImportValidatedPlan(string path)
    {
        busy=true;
        try
        {
            if(!PlanPersistence.TryImport(path,out var imported,out _,out var error)){status="Không thể nhập dữ liệu: "+error;return;}
            plan=imported;EnsurePlanDefaults();undo.Clear();redo.Clear();selectedId=null;placementMode=false;layer=0;status="Đã nhập phương án "+plan.name+".";screen=ScreenMode.Detail;Rebuild();ResetCamera();
        }
        finally{busy=false;if(mobileCanvas)RefreshMobileUI();}
    }

    void DrawHome()
    {
        GUILayout.Label("Container",title);
        GUILayout.Label("Theo dõi và tiếp tục các phương án xếp hàng tại một nơi.",muted);
        GUILayout.Space(10);
        if(GUILayout.Button("+  Thêm container",primaryButton))BeginNewContainer();
        GUILayout.BeginHorizontal();
        search=GUILayout.TextField(search,input,GUILayout.ExpandWidth(true));
        if(GUILayout.Button(sortNewest?"Mới nhất":"Tên A–Z",button,GUILayout.Width(110))){sortNewest=!sortNewest;}
        GUILayout.EndHorizontal();
        var entries=PlanPersistence.ListFiles().Select(path=>new{path,data=PlanPersistence.Load(path)}).Where(x=>x.data!=null&&(string.IsNullOrWhiteSpace(search)||x.data.name.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0||x.data.container.name.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0));
        entries=sortNewest?entries.OrderByDescending(x=>File.GetLastWriteTimeUtc(x.path)):entries.OrderBy(x=>x.data.name);
        var list=entries.ToList();
        if(list.Count==0)
        {
            GUILayout.Space(35);GUILayout.BeginVertical(card);
            GUILayout.Label("Chưa có container",heading);
            GUILayout.Label(string.IsNullOrWhiteSpace(search)?"Mỗi container bạn tạo sẽ xuất hiện tại đây.":"Không tìm thấy container phù hợp.",muted);
            if(string.IsNullOrWhiteSpace(search)&&GUILayout.Button("+  Tạo container đầu tiên",primaryButton))BeginNewContainer();
            GUILayout.EndVertical();return;
        }
        foreach(var entry in list)DrawContainerCard(entry.path,entry.data);
    }
    void DrawContainerCard(string path,LoadingPlan data)
    {
        var used=UsedVolume(data);var capacity=Mathf.Max(1,data.container.length*data.container.width*data.container.height);var percent=Mathf.Clamp(Mathf.RoundToInt(used*100f/capacity),0,100);
        GUILayout.BeginVertical(card);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        GUILayout.Label(data.name,title);
        GUILayout.Label($"{data.container.name}  ·  {data.container.length} × {data.container.width} × {data.container.height}",muted);
        GUILayout.EndVertical();
        GUILayout.Label(percent==0?"CHƯA BẮT ĐẦU":percent>=100?"HOÀN THÀNH":"ĐANG XẾP",badge,GUILayout.Width(112));
        GUILayout.EndHorizontal();
        GUILayout.Space(8);GUILayout.Label($"{data.placedCargo.Count} thùng  ·  {data.cargoTypes.Count} loại hàng  ·  {percent}% dung tích",label);
        var time=File.GetLastWriteTime(path);GUILayout.Label("Cập nhật: "+time.ToString("dd/MM/yyyy HH:mm"),muted);
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Mở",primaryButton))OpenPlan(path);
        if(GUILayout.Button("Nhân bản",button))DuplicatePlan(data);
        if(GUILayout.Button("Xóa",button,GUILayout.Width(72)))deletePlanPath=path;
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }
    int UsedVolume(LoadingPlan data)=>data.placedCargo.Sum(x=>x.size.x*x.size.y*x.size.z);
    void OpenPlan(string path){if(!PlanPersistence.TryLoad(path,out var loaded,out var error)){status="Không thể mở phương án: "+error;if(mobileCanvas)RefreshMobileUI();return;}plan=loaded;EnsurePlanDefaults();undo.Clear();redo.Clear();selectedId=null;placementMode=false;layer=0;status="Đã mở "+plan.name+".";screen=ScreenMode.Detail;Rebuild();ResetCamera();scroll=Vector2.zero;}
    void DuplicatePlan(LoadingPlan source,bool includePlacements=true){try{var copy=JsonUtility.FromJson<LoadingPlan>(JsonUtility.ToJson(source));var baseName=source.name+"_COPY";copy.name=baseName;var suffix=2;while(PlanPersistence.ListFiles().Any(x=>string.Equals(Path.GetFileNameWithoutExtension(x),PlanPersistence.SafeName(copy.name),StringComparison.OrdinalIgnoreCase)))copy.name=baseName+"_"+suffix++;copy.container.id=Guid.NewGuid().ToString("N");copy.containerNumber="";copy.sealNumber="";copy.orderReference="";copy.loadingDate="";if(!includePlacements)copy.placedCargo.Clear();copy.createdAt=DateTime.UtcNow.ToString("O");copy.updatedAt=copy.createdAt;PlanPersistence.Save(copy);status="Đã nhân bản "+source.name+(includePlacements?" kèm sơ đồ.":" chỉ dữ liệu hàng.");}catch(Exception exception){Debug.LogException(exception);status="Không thể nhân bản phương án.";}if(mobileCanvas)RefreshMobileUI();}

    void BeginNewContainer(){editingContainer=false;formDirty=false;containerCode="CONT"+DateTime.Now.ToString("MMddHHmm");containerName="Container tùy chỉnh";containerLength="10";containerWidth="3";containerHeight="6";orderReference=customerName=containerNumber=sealNumber=destination=loadingDate=orderNotes="";status="Nhập kích thước theo đơn vị ô lưới.";screen=ScreenMode.Container;scroll=Vector2.zero;}
    void BeginEditContainer(){editingContainer=true;formDirty=false;editingOriginalPath=PlanPersistence.ListFiles().FirstOrDefault(x=>string.Equals(Path.GetFileNameWithoutExtension(x),PlanPersistence.SafeName(plan.name),StringComparison.OrdinalIgnoreCase));containerCode=plan.name;containerName=plan.container.name;containerLength=plan.container.length.ToString();containerWidth=plan.container.width.ToString();containerHeight=plan.container.height.ToString();orderReference=plan.orderReference;customerName=plan.customerName;containerNumber=plan.containerNumber;sealNumber=plan.sealNumber;destination=plan.destination;loadingDate=plan.loadingDate;orderNotes=plan.orderNotes;status=plan.placedCargo.Count>0?"Thay đổi kích thước không được làm thùng hiện tại vượt giới hạn.":"Có thể chỉnh toàn bộ thông tin container.";screen=ScreenMode.Container;scroll=Vector2.zero;}
    void DrawContainer()
    {
        GUILayout.Label(editingContainer?"Chỉnh sửa container":"Thêm container",title);
        GUILayout.Label("Thông tin cơ bản",muted);Notice();
        Section("Kích thước nhanh");
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("20FT",button))Preset("20FT",6,3,3);
        if(GUILayout.Button("40FT",button))Preset("40FT",12,3,3);
        if(GUILayout.Button("40FT HC",button))Preset("40FT HC",12,3,4);
        if(GUILayout.Button("Tùy chỉnh",button))containerName="Container tùy chỉnh";
        GUILayout.EndHorizontal();
        Section("Thông tin container");
        GUILayout.BeginVertical(card);
        Field("Tên / mã container",ref containerCode);Field("Loại / mô tả",ref containerName);Field("Mã đơn hàng / tham chiếu",ref orderReference);Field("Tên khách hàng",ref customerName);GUILayout.Label("Ghi chú đơn hàng",label);orderNotes=GUILayout.TextArea(orderNotes??"",input,GUILayout.MinHeight(100));
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();Field("Dài (X)",ref containerLength);GUILayout.EndVertical();
        GUILayout.BeginVertical();Field("Rộng (Y)",ref containerWidth);GUILayout.EndVertical();
        GUILayout.BeginVertical();Field("Cao (Z)",ref containerHeight);GUILayout.EndVertical();
        GUILayout.EndHorizontal();GUILayout.EndVertical();
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Hủy",button)){if(editingContainer)OpenDetail();else OpenHome();}
        if(GUILayout.Button(editingContainer?"Lưu thay đổi":"Tạo container",primaryButton))SaveContainer();
        GUILayout.EndHorizontal();
    }
    void Preset(string name,int l,int w,int h){containerName=name;containerLength=l.ToString();containerWidth=w.ToString();containerHeight=h.ToString();}
    void SaveContainer()
    {
        var code=(containerCode??"").Trim();var description=(containerName??"").Trim();var reference=(orderReference??"").Trim();var customer=(customerName??"").Trim();var notes=(orderNotes??"").Trim();
        if(string.IsNullOrWhiteSpace(code)||code.Length>PlanValidation.MaxTextLength||code.Contains('\n')||code.Contains('\r')){status="Mã phương án là bắt buộc, tối đa 200 ký tự và không chứa xuống dòng.";return;}
        if(description.Length>PlanValidation.MaxTextLength||reference.Length>PlanValidation.MaxTextLength||customer.Length>PlanValidation.MaxTextLength||notes.Length>PlanValidation.MaxNotesLength){status="Thông tin nhập quá dài. Mã/tên tối đa 200 ký tự, ghi chú tối đa 20.000 ký tự.";return;}
        if(!Dimension(containerLength,out var l)||!Dimension(containerWidth,out var w)||!Dimension(containerHeight,out var h)){status=$"Kích thước container phải là số nguyên từ 1 đến {PlanValidation.MaxDimension}.";return;}
        if(editingContainer)
        {
            if(!PlanValidation.CanResize(plan,new Vector3Int(l,w,h),out var resizeError)){status="Không thể thay đổi kích thước: "+resizeError;return;}
            plan.name=code;plan.container.name=string.IsNullOrWhiteSpace(description)?"Container":description;plan.container.length=l;plan.container.width=w;plan.container.height=h;plan.orderReference=reference;plan.customerName=customer;plan.containerNumber=(containerNumber??"").Trim();plan.sealNumber=(sealNumber??"").Trim();plan.destination=(destination??"").Trim();plan.loadingDate=(loadingDate??"").Trim();plan.orderNotes=notes;status="Đã cập nhật container.";
        }
        else
        {
            plan=new LoadingPlan{name=code,orderReference=reference,customerName=customer,containerNumber=(containerNumber??"").Trim(),sealNumber=(sealNumber??"").Trim(),destination=(destination??"").Trim(),loadingDate=(loadingDate??"").Trim(),orderNotes=notes,container=new ContainerConfig{id=Guid.NewGuid().ToString("N"),name=string.IsNullOrWhiteSpace(description)?"Container":description,length=l,width=w,height=h}};
            undo.Clear();redo.Clear();selectedId=null;layer=0;status="Container đã tạo. Hãy thêm loại thùng hàng.";
        }
        if(!TrySaveCurrent(editingContainer?editingOriginalPath:null))return;editingOriginalPath=null;formDirty=false;screen=ScreenMode.Detail;Rebuild();ResetCamera();scroll=Vector2.zero;
    }

    void DrawDetail()
    {
        GUILayout.Label(plan.name,title);GUILayout.Label($"{plan.container.name}  ·  {plan.container.length} × {plan.container.width} × {plan.container.height}",muted);if(!string.IsNullOrWhiteSpace(plan.orderReference))GUILayout.Label("Đơn hàng: "+plan.orderReference,label);if(!string.IsNullOrWhiteSpace(plan.customerName))GUILayout.Label("Khách hàng: "+plan.customerName,label);if(!string.IsNullOrWhiteSpace(plan.orderNotes))GUILayout.Label("Ghi chú: "+plan.orderNotes,muted);Notice();
        var used=UsedVolume(plan);var capacity=Mathf.Max(1,plan.container.length*plan.container.width*plan.container.height);var percent=Mathf.Clamp(Mathf.RoundToInt(used*100f/capacity),0,100);
        Section("Tổng quan");
        GUILayout.BeginHorizontal();
        Stat("Tổng thùng",plan.placedCargo.Count.ToString());Stat("Loại hàng",plan.cargoTypes.Count.ToString());Stat("Đã chiếm",percent+"%");Stat("Còn trống",(capacity-used).ToString()+" ô");
        GUILayout.EndHorizontal();
        GUILayout.BeginVertical(card);GUILayout.Label(percent==0?"Chưa bắt đầu":percent>=100?"Hoàn thành":"Đang xếp",heading);GUILayout.Label($"Đã sử dụng {used} / {capacity} ô thể tích",muted);GUILayout.HorizontalSlider(percent,0,100);GUILayout.EndVertical();
        if(plan.cargoTypes.Count==0)GUILayout.Label("Chưa có loại hàng. Hãy khai báo kích thước thùng trước khi xếp.",label);
        GUI.enabled=plan.cargoTypes.Count>0;if(GUILayout.Button(plan.placedCargo.Count==0?"Bắt đầu xếp hàng":"Tiếp tục xếp hàng",primaryButton)){screen=ScreenMode.Editor;suppressEditorPointer=true;scroll=Vector2.zero;status="Chọn loại hàng rồi chạm vào lưới.";ResetCamera();}GUI.enabled=true;
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Loại hàng",button)){screen=ScreenMode.Cargo;scroll=Vector2.zero;ClearCargo();}
        if(GUILayout.Button("Chỉnh sửa",button))BeginEditContainer();
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Xuất PDF",button))ExportPdf();
        GUI.enabled=!string.IsNullOrEmpty(lastPdf)&&File.Exists(lastPdf);if(GUILayout.Button("Xem PDF",button))OpenPdf();if(GUILayout.Button("Chia sẻ",button))SharePdf();GUI.enabled=true;
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();if(GUILayout.Button("Nhập JSON",button))BeginImportPlan();if(GUILayout.Button("Sao lưu JSON",button))ExportPlanData();GUILayout.EndHorizontal();
        if(GUILayout.Button("Xóa container",dangerButton))deletePlanPath=PlanPersistence.ListFiles().FirstOrDefault(x=>string.Equals(Path.GetFileNameWithoutExtension(x),plan.name,StringComparison.OrdinalIgnoreCase));
    }
    void Stat(string caption,string value){GUILayout.BeginVertical(card,GUILayout.MinWidth(100));GUILayout.Label(value,heading);GUILayout.Label(caption,muted);GUILayout.EndVertical();}

    void DrawCargo()
    {
        GUILayout.Label("Loại thùng hàng",title);GUILayout.Label($"{plan.name}  ·  Kích thước do người dùng nhập",muted);Notice();
        Section("Danh sách");
        if(plan.cargoTypes.Count==0)GUILayout.Label("Chưa có loại hàng. Tạo loại đầu tiên bên dưới.",label);
        foreach(var type in plan.cargoTypes.ToArray())
        {
            GUILayout.BeginHorizontal(card);var old=GUI.backgroundColor;GUI.backgroundColor=type.color;GUILayout.Box("",GUILayout.Width(18),GUILayout.Height(50));GUI.backgroundColor=old;
            GUILayout.BeginVertical();GUILayout.Label(type.code+"  ·  "+type.name,heading);GUILayout.Label($"{type.length} × {type.width} × {type.height} · Đã xếp {plan.placedCargo.Count(x=>x.cargoTypeId==type.id)} kiện",muted);GUILayout.EndVertical();
            if(GUILayout.Button("Sửa",button,GUILayout.Width(66)))EditCargo(type);
            if(GUILayout.Button("Xóa",button,GUILayout.Width(66)))RequestDeleteCargo(type);
            GUILayout.EndHorizontal();
        }
        Section(editingTypeId==null?"Thêm loại hàng":"Sửa loại hàng");
        GUILayout.BeginVertical(card);Field("Mã hàng",ref cargoCode);Field("Tên hàng",ref cargoName);
        GUILayout.BeginHorizontal();GUILayout.BeginVertical();Field("Dài",ref cargoLength);GUILayout.EndVertical();GUILayout.BeginVertical();Field("Rộng",ref cargoWidth);GUILayout.EndVertical();GUILayout.BeginVertical();Field("Cao",ref cargoHeight);GUILayout.EndVertical();GUILayout.EndHorizontal();
        GUILayout.Label("MÀU LOẠI HÀNG  #"+ColorUtility.ToHtmlStringRGB(cargoColor),label);
        var colorPreview=GUI.backgroundColor;GUI.backgroundColor=cargoColor;GUILayout.Box("",GUILayout.Height(32));GUI.backgroundColor=colorPreview;
        cargoColor.r=GUILayout.HorizontalSlider(cargoColor.r,0,1);cargoColor.g=GUILayout.HorizontalSlider(cargoColor.g,0,1);cargoColor.b=GUILayout.HorizontalSlider(cargoColor.b,0,1);
        if(GUILayout.Button(editingTypeId==null?"Thêm loại hàng":"Lưu thay đổi",primaryButton))SaveCargo();
        if(editingTypeId!=null&&GUILayout.Button("Hủy chỉnh sửa",button))ClearCargo();GUILayout.EndVertical();
        GUILayout.BeginHorizontal();if(GUILayout.Button("Quay lại chi tiết",button))OpenDetail();GUI.enabled=plan.cargoTypes.Count>0;if(GUILayout.Button("Mở trình xếp 3D",primaryButton)&&TrySaveCurrent()){screen=ScreenMode.Editor;suppressEditorPointer=true;scroll=Vector2.zero;ResetCamera();}GUI.enabled=true;GUILayout.EndHorizontal();
    }
    void EditCargo(CargoType type){cargoFormVisible=true;editingTypeId=type.id;cargoCode=type.code;cargoName=type.name;cargoLength=type.length.ToString();cargoWidth=type.width.ToString();cargoHeight=type.height.ToString();cargoQuantity=type.quantity.ToString();cargoWeight=type.weightPerUnit.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);cargoAllowRotation=type.allowRotation;cargoStackable=type.stackable;cargoColor=type.color;}
    void SaveCargo()
    {
        var code=(cargoCode??"").Trim();var name=(cargoName??"").Trim();
        if(string.IsNullOrWhiteSpace(code)||code.Length>PlanValidation.MaxTextLength||code.Contains('\n')||code.Contains('\r')){status="Mã hàng là bắt buộc, tối đa 200 ký tự và không chứa xuống dòng.";return;}
        if(name.Length>PlanValidation.MaxTextLength||name.Contains('\n')||name.Contains('\r')){status="Tên hàng tối đa 200 ký tự và không chứa xuống dòng.";return;}
        if(!Dimension(cargoLength,out var l)||!Dimension(cargoWidth,out var w)||!Dimension(cargoHeight,out var h)){status=$"Kích thước hàng phải là số nguyên từ 1 đến {PlanValidation.MaxDimension}.";return;}
        if(!int.TryParse(cargoQuantity,out var quantity)||quantity<1||quantity>PlanValidation.MaxPlacedCargo){status=$"Số lượng phải từ 1 đến {PlanValidation.MaxPlacedCargo}.";return;}
        if(!float.TryParse((cargoWeight??"0").Replace(',','.'),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var weight)||weight<0||weight>10000000){status="Trọng lượng mỗi kiện không hợp lệ.";return;}
        if(h>plan.container.height||(l>plan.container.length||w>plan.container.width)&&(w>plan.container.length||l>plan.container.width)){status="Kích thước loại hàng không thể nằm vừa trong container.";return;}
        if(plan.cargoTypes.Exists(x=>string.Equals(x.code,code,StringComparison.OrdinalIgnoreCase)&&x.id!=editingTypeId)){status="Mã hàng đã tồn tại.";return;}
        var backup=JsonUtility.ToJson(plan);var type=editingTypeId==null?new CargoType{id=Guid.NewGuid().ToString("N")}:plan.cargoTypes.Find(x=>x.id==editingTypeId);if(type==null){ClearCargo();return;}
        if(editingTypeId!=null&&plan.placedCargo.Exists(x=>x.cargoTypeId==editingTypeId)&&(type.length!=l||type.width!=w||type.height!=h)){status="Không thể đổi kích thước vì loại hàng này đã được đặt. Hãy xóa các thùng đã đặt trước.";return;}
        var placedCount=plan.placedCargo.Count(x=>x.cargoTypeId==type.id);if(quantity<placedCount){status=$"Không thể giảm số lượng xuống {quantity} vì đã xếp {placedCount} kiện.";return;}
        type.code=code;type.name=string.IsNullOrWhiteSpace(name)?code:name;type.length=l;type.width=w;type.height=h;type.quantity=quantity;type.weightPerUnit=weight;type.color=cargoColor;type.allowRotation=cargoAllowRotation;type.stackable=cargoStackable;if(editingTypeId==null)plan.cargoTypes.Add(type);
        if(!TrySaveCurrent()){plan=JsonUtility.FromJson<LoadingPlan>(backup);return;}status="Đã lưu loại hàng "+type.code+".";ClearCargo();Rebuild();
    }
    void RequestDeleteCargo(CargoType type){if(plan.placedCargo.Exists(x=>x.cargoTypeId==type.id)){status="Không thể xóa vì loại hàng này đang được sử dụng trong sơ đồ.";return;}deleteCargoTarget=type;}
    void ClearCargo(){editingTypeId=null;cargoFormVisible=false;formDirty=false;cargoCode=cargoName="";cargoLength=cargoWidth=cargoHeight=cargoQuantity="1";cargoWeight="0";cargoAllowRotation=cargoStackable=true;cargoColor=palette[0];}
    int ClosestColor(Color color){var best=0;var distance=float.MaxValue;for(var i=0;i<palette.Length;i++){var delta=palette[i]-color;var d=delta.r*delta.r+delta.g*delta.g+delta.b*delta.b;if(d<distance){distance=d;best=i;}}return best;}
    static bool Positive(string value,out int result)=>int.TryParse(value,out result)&&result>0;
    static bool Dimension(string value,out int result)=>int.TryParse((value??"").Trim(),out result)&&PlanValidation.ValidDimension(result);

    void DrawEditor()
    {
        GUI.DrawTexture(new Rect(0,0,PanelWidth,ViewHeight),whiteTex);
        GUILayout.BeginArea(new Rect(12,12,PanelWidth-24,ViewHeight-24));scroll=GUILayout.BeginScrollView(scroll,false,false);
        if(GUILayout.Button("←  Chi tiết container",button))OpenDetail();
        GUILayout.Label(plan.name,title);GUILayout.Label($"{plan.placedCargo.Count} thùng  ·  Tầng {layer+1}/{plan.container.height}",muted);Notice();
        Section("Hàng đang chọn");
        for(var i=0;i<plan.cargoTypes.Count;i++){var type=plan.cargoTypes[i];var old=GUI.backgroundColor;GUI.backgroundColor=i==selectedType?accentTex.GetPixel(0,0):type.color;if(GUILayout.Button($"{type.code}  ·  {type.name}\n{type.length} × {type.width} × {type.height}",i==selectedType?primaryButton:button)){selectedType=i;status="Đã chọn "+type.code+".";}GUI.backgroundColor=old;}
        Section("Tầng");GUILayout.BeginHorizontal();if(GUILayout.Button("−",button))layer=Mathf.Max(0,layer-1);GUILayout.Label($"Tầng {layer+1}",badge,GUILayout.Width(90));if(GUILayout.Button("+",button))layer=Mathf.Min(plan.container.height-1,layer+1);GUILayout.EndHorizontal();
        Section("Thùng đang chọn");
        var selected=FindPlaced(selectedId);if(selected==null)GUILayout.Label("Chạm một thùng để xem vị trí và thao tác.",muted);else GUILayout.Label($"X {selected.position.x+1}  ·  Y {selected.position.y+1}  ·  Z {selected.position.z+1}",label);
        GUILayout.BeginHorizontal();if(GUILayout.Button("Xoay",button))RotateSelected();if(GUILayout.Button("Di chuyển",button))MoveSelected();if(GUILayout.Button("Xóa",button))DeleteSelected();GUILayout.EndHorizontal();
        Section("Công cụ");GUILayout.BeginHorizontal();if(GUILayout.Button("Hoàn tác",button))Undo();if(GUILayout.Button("Làm lại",button))Redo();GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();if(GUILayout.Button("3D",button))ResetCamera();if(GUILayout.Button("Top",button))TopView();if(GUILayout.Button("Front",button))FrontView();if(GUILayout.Button("Side",button))SideView();GUILayout.EndHorizontal();
        if(GUILayout.Button("Lưu sơ đồ",primaryButton))TrySaveCurrent();
        GUILayout.EndScrollView();GUILayout.EndArea();
    }

    void DrawSettings()
    {
        GUILayout.Label("Cài đặt",title);GUILayout.Label("Thiết lập tối thiểu cho môi trường vận hành.",muted);
        Section("Đơn vị");GUILayout.BeginVertical(card);GUILayout.Label("Đơn vị xếp hàng",heading);GUILayout.Label("Ô lưới nguyên (X × Y × Z)",muted);GUILayout.EndVertical();
        Section("PDF");GUILayout.BeginVertical(card);GUILayout.Label("Báo cáo tiêu chuẩn",heading);GUILayout.Label("Top, Side, Front, danh sách hàng và thống kê.",muted);GUILayout.EndVertical();
        Section("Giới thiệu");GUILayout.BeginVertical(card);GUILayout.Label("Container Loading",heading);GUILayout.Label("Phiên bản 1.0.0\nDữ liệu được lưu cục bộ trên thiết bị.",muted);GUILayout.EndVertical();
    }

    void DrawConfirmation()
    {
        if(string.IsNullOrEmpty(deletePlanPath)&&deleteCargoTarget==null)return;
        GUI.color=new Color(0,0,0,.45f);GUI.DrawTexture(new Rect(0,0,ViewWidth,ViewHeight),whiteTex);GUI.color=Color.white;
        var width=Mathf.Min(420,ViewWidth-36);GUILayout.BeginArea(new Rect((ViewWidth-width)*.5f,(ViewHeight-230)*.5f,width,230),card);
        GUILayout.Label(deleteCargoTarget==null?"Xóa container?":"Xóa loại hàng?",title);
        GUILayout.Label(deleteCargoTarget==null?"Toàn bộ sơ đồ xếp hàng của container này sẽ bị xóa.":"Loại hàng này sẽ bị xóa khỏi danh mục.",label);
        GUILayout.FlexibleSpace();GUILayout.BeginHorizontal();
        if(GUILayout.Button("Hủy",button)){deletePlanPath=null;deleteCargoTarget=null;}
        if(GUILayout.Button("Xóa",dangerButton))
        {
            if(deleteCargoTarget!=null){var backup=JsonUtility.ToJson(plan);plan.cargoTypes.Remove(deleteCargoTarget);if(TrySaveCurrent()){deleteCargoTarget=null;status="Đã xóa loại hàng.";}else plan=JsonUtility.FromJson<LoadingPlan>(backup);}
            else{PlanPersistence.Delete(deletePlanPath);deletePlanPath=null;status="Đã xóa container.";OpenHome();}
        }
        GUILayout.EndHorizontal();GUILayout.EndArea();
    }
}
