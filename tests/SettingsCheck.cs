using System; using System.Reflection; using System.Linq; using System.Collections; using System.Collections.Generic; using System.Windows.Forms; using Microsoft.Win32; using System.IO; using System.Runtime.Versioning;
[assembly: TargetFramework(".NETFramework,Version=v4.7.2")]
class SettingsCheck {
 static Type options; static string root; static int passed;
 static object Field(object o,string name){return o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
 static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);}
 static Form Open(){return (Form)Activator.CreateInstance(options,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{root},null);}
 static void Expect(bool condition,string text){if(!condition)throw new Exception(text);Console.WriteLine("PASS "+text);passed++;}
 static int Count(object plan){return ((ICollection)plan).Count;}
 static object Plan(Form f,bool paths=false){return Call(f,"PrepareSave",paths);}
 static void Commit(Form f,object plan,Action<int> fault=null){Call(Field(f,"profile"),"Commit",plan,fault);}
 static object Read(string section,string name){using(var k=Registry.CurrentUser.OpenSubKey(root+"\\"+section))return k==null?null:k.GetValue(name);}
 static void Write(string section,string name,object value){using(var k=Registry.CurrentUser.CreateSubKey(root+"\\"+section)) k.SetValue(name,value,value is byte[]?RegistryValueKind.Binary:value is int?RegistryValueKind.DWord:RegistryValueKind.String);}
 static void Reset(){Registry.CurrentUser.DeleteSubKeyTree(root,false);}
 static void Throws(Action action,string text){bool failed=false;try{action();}catch(TargetInvocationException e){failed=e.InnerException is InvalidOperationException;}Expect(failed,text);}
 [STAThread] static int Main(string[] a){
 root=@"Software\EIStarterValidation\settings-"+Guid.NewGuid().ToString("N"); options=Assembly.LoadFrom(a[0]).GetType("EIStarter.Options");
 try {
  using(var f=Open()){
   Expect(Count(Plan(f))==0,"empty profile is a no-op");
   Expect(((ComboBox)Field(f,"cbbResolutions")).SelectedItem==null,"missing resolution remains unspecified");
   Expect(((CheckBox)Field(f,"cbMPConsoleSupport")).CheckState==CheckState.Indeterminate,"missing flag is indeterminate");
   var model=((PropertyGrid)Field(f,"propGridCamera")).SelectedObject;
   Expect(float.IsNaN((float)model.GetType().GetProperty("FarClipDistance").GetValue(model,null)),"missing camera value is unspecified");
   ((CheckBox)Field(f,"cbMPConsoleSupport")).CheckState=CheckState.Unchecked;
   var plan=Plan(f); Expect(Count(plan)==1,"explicit false saves one value even with no resolution"); Commit(f,plan);
   Expect((int)Read("settings","MP console")==0 && Read(@"EvilIslands\camera settings","FarClipDistance")==null && Read(@"EvilIslands\general settings","fullscreen")==null && Read(@"EvilIslands\general settings","mipmapping")==null,"single multiplayer edit creates no camera or obsolete defaults");
  }
  Reset();
  foreach(int radius in new[]{10,5000}){
   Write("settings","ObjectsDrawRadius",radius); Write("settings","LandscapeDrawRadius",radius);
   using(var f=Open()) {Expect(Count(Plan(f))==0,"retired radius boundary "+radius+" is a no-op"); ((ComboBox)Field(f,"cbbAudio")).SelectedIndex=2; Commit(f,Plan(f)); Expect((int)Read("settings","ObjectsDrawRadius")==radius && (int)Read("settings","LandscapeDrawRadius")==radius,"sound save preserves both retired radius boundaries "+radius);}
  }
  Reset(); Write(@"EvilIslands\general settings","TextureQuality",99); Write(@"EvilIslands\terrain settings","LIGHTINGFREQ",1234);
  using(var f=Open()){Expect(Count(Plan(f))==0,"retired custom values are a no-op"); ((CheckBox)Field(f,"cbMPConsoleSupport")).Checked=true; Commit(f,Plan(f)); Expect((int)Read(@"EvilIslands\general settings","TextureQuality")==99 && (int)Read(@"EvilIslands\terrain settings","LIGHTINGFREQ")==1234,"unrelated save preserves retired custom enum and interval");}
  Reset();
  using(var f=Open()) {
   ((CheckBox)Field(f,"cbMPConsoleSupport")).CheckState=CheckState.Checked;
   ((ComboBox)Field(f,"cbbAudio")).SelectedIndex=2;
   var plan=Plan(f); bool failed=false;
   try {Commit(f,plan,n=>{if(n==1)throw new IOException("injected");});}catch(TargetInvocationException){failed=true;}
   Expect(failed && Read("settings","MP console")==null && Read(@"EvilIslands\general settings","audio")==null,"injected failure rolls back every value");
   Commit(f,plan); Expect((int)Read("settings","MP console")==1 && (int)Read(@"EvilIslands\general settings","audio")==2,"transaction commits complete multi-section plan");
  }
  Reset();
  using(var f=Open()){
   ((CheckBox)Field(f,"cbMPConsoleSupport")).CheckState=CheckState.Checked; var plan=Plan(f);
   Write("settings","MP console",0);
   Throws(()=>Commit(f,plan),"external conflict rejected"); Expect((int)Read("settings","MP console")==0,"external setting preserved");
  }
  Reset();
  using(var f=Open()) {
   var m=((PropertyGrid)Field(f,"propGridCamera")).SelectedObject;
   m.GetType().GetProperty("NearClipDistance").SetValue(m,5f,null); m.GetType().GetProperty("FarClipDistance").SetValue(m,2f,null);
   Throws(()=>Plan(f),"invalid clip ordering rejected before writes");
   m.GetType().GetProperty("FarClipDistance").SetValue(m,float.PositiveInfinity,null); Throws(()=>Plan(f),"infinite camera value rejected");
   m.GetType().GetProperty("FarClipDistance").SetValue(m,200f,null); Expect(Count(Plan(f))==2,"valid camera pair accepted");
   Expect(Read(@"EvilIslands\camera settings","FarClipDistance")==null,"validation alone never writes");
  }
  Reset();
  using(var f=Open()){
   ((TextBox)Field(f,"NTRtbPathCD")).Text=Path.GetTempPath(); ((TextBox)Field(f,"NTRtbPathRes")).Text=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString());
   Throws(()=>Plan(f,true),"invalid path rejects whole path plan"); Expect(Read(@"EvilIslands\Path Settings","CD-ROM Path")==null,"invalid path plan leaves other paths untouched");
  }
  Reset();
  using(var f=Open()){
   Call(f,"BuildSettings");
   var controls=Walk(f).ToArray();
   var sections=controls.Where(c=>c.Name.StartsWith("section_")).ToArray();
   Expect(sections.Length==4 && sections.Select(c=>c.Text).SequenceEqual(new[]{"tabGraphics","tabMultiplayer","tabCamera","tabPaths"}.Select(name=>((TabPage)Field(f,name)).Text)),"four supported sections in correct order, no Addon");
   Expect(new[]{"toggle_cbMPConsoleSupport","toggle_cbMPDisableMQ","toggle_cbMPSecondSpeed"}.All(name=>Walk(controls.First(c=>c.Name=="settingsViewport_1")).Any(c=>c.Name==name)),"multiplayer controls remain on Multiplayer");
   Expect(Walk(controls.First(c=>c.Name=="settingsViewport_2")).Any(c=>c.Name=="camera_FarClipDistance"),"camera controls remain on Camera");
   Expect(Walk(controls.First(c=>c.Name=="settingsViewport_3")).Any(c=>c.Name=="NTRtbPathCD"),"path controls remain on Paths");
   Expect(!controls.Any(c=>c.Name=="toggle_cbOnMinimizePause" || c.Name=="toggle_cbCharFixWinXP"),"unsupported toggles absent");
   Expect(!controls.Any(c=>c.Name=="choice_cbbAdapter"),"unverified GPU ordinal picker absent");
   Expect(!controls.Any(c=>c.Name=="toggle_cbAntialiasing" || c.Name=="toggle_cbDithering" || c.Name=="choice_cbbShadowing"),"obsolete AA, dithering and shadow interval controls absent");
   Expect(!controls.Any(c=>c.Name=="choice_cbbShadows"),"original game has no software shadow picker");
   Expect(!controls.Any(c=>c.Name=="toggle_cbFullscreen" || c.Name=="toggle_cbMipMapping"),"legacy fullscreen and mip controls absent");
   Expect(!new[]{"toggle_cbFPSCursor","choice_cbbFiltering","choice_cbbTextures","choice_cbbTerrain","choice_cbbLighting","choice_cbbMovies"}.Any(name=>controls.Any(c=>c.Name==name)),"all six owner-selected game controls absent");
   Expect(sections.All(c=>c.GetType().Name=="SettingsSectionButton") && sections.Select(c=>(string)c.GetType().GetProperty("Section").GetValue(c,null)).SequenceEqual(new[]{"tabGraphics","tabMultiplayer","tabCamera","tabPaths"}),"all four sections have the correct drawn icon");
   Expect(controls.First(c=>c.Name=="choice_cbbResolutions").Controls.Contains((ComboBox)Field(f,"cbbResolutions")),"resolution uses the original in-page dropdown and binding");
   var sound=controls.First(c=>c.Name=="choice_cbbAudio");
   Expect(sound is FlowLayoutPanel && sound.Controls.Count==3,"sound remains compact inline choices");
   typeof(Button).GetMethod("OnClick",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(sound.Controls[2],new object[]{EventArgs.Empty});
   Expect(((ComboBox)Field(f,"cbbAudio")).SelectedIndex==2 && Count(Plan(f))==1 && Read(@"EvilIslands\general settings","audio")==null,"sound button prepares one edit without immediate write");
   Expect(new[]{"choice_cbbResolutions","choice_cbbAudio"}.All(name=>controls.Any(c=>c.Name==name)),"startup resolution and sound remain available");
   Expect(!Walk(controls.First(c=>c.Name=="settingsViewport_0")).Any(c=>c.Name=="nbDrawRadiusObjects" || c.Name=="nbDrawRadiusTerrain"),"retired Addon radius editors are not relocated to Graphics");
   var page=((Panel)controls.First(c=>c.Name=="settingsViewport_0")).Controls[0];
   Expect(page.Controls.GetChildIndex(controls.First(c=>c.Name=="gameSettingsHeading"))<page.Controls.GetChildIndex(controls.First(c=>c.Name=="ownedRendererF10Hint")),"game settings precede renderer and F10 guidance");
  }
  Reset();
  Write(@"EvilIslands\general settings","antialiasing",99);
  byte[] customDither=new byte[]{7,9,11}; Write(@"EvilIslands\general settings","dithering",customDither);
  Write(@"EvilIslands\shadow settings","shadowquality",2);
  Write(@"EvilIslands\terrain settings","SHADOWINGFREQ",1234);
  Write(@"EvilIslands\general settings","fullscreen",1);
  byte[] customMips=new byte[]{3,5,7}; Write(@"EvilIslands\general settings","mipmapping",customMips);
  Write(@"EvilIslands\general settings","FPSIndependentCursor",1);
  Write(@"EvilIslands\general settings","TextureQuality",99);
  byte[] customFilter=new byte[]{2,4,6}; Write(@"EvilIslands\general settings","filtering",customFilter);
  Write(@"EvilIslands\general settings","video","custom movies");
  Write(@"EvilIslands\terrain settings","EnableWaterWaves",1);
  byte[] customLod1=BitConverter.GetBytes(72.5f), customLod2=new byte[]{1,7,9};
  Write(@"EvilIslands\terrain settings","LOD1",customLod1); Write(@"EvilIslands\terrain settings","LOD2",customLod2);
  Write(@"EvilIslands\terrain settings","LIGHTINGFREQ",1234);
  Write("settings","ObjectsDrawRadius",10); Write("settings","LandscapeDrawRadius",5000);
  using(var f=Open()){
   Call(f,"BuildSettings");
   ((CheckBox)Field(f,"cbAntialiasing")).Checked=true;
   ((CheckBox)Field(f,"cbDithering")).Checked=true;
   ((CheckBox)Field(f,"cbFullscreen")).Checked=false;
   ((CheckBox)Field(f,"cbMipMapping")).Checked=true;
   ((ComboBox)Field(f,"cbbShadowing")).SelectedIndex=0;
   ((ComboBox)Field(f,"cbbShadows")).SelectedIndex=0;
   ((CheckBox)Field(f,"cbFPSCursor")).Checked=false;
   foreach(var name in new[]{"cbbTextures","cbbFiltering","cbbTerrain","cbbLighting","cbbMovies"}) ((ComboBox)Field(f,name)).SelectedIndex=0;
   ((NumericUpDown)Field(f,"nbDrawRadiusObjects")).Value=300; ((NumericUpDown)Field(f,"nbDrawRadiusTerrain")).Value=400;
   Expect(Count(Plan(f))==0,"removed controls cannot edit legacy keys");
   ((ComboBox)Field(f,"cbbAudio")).SelectedIndex=2;
   var plan=Plan(f); Expect(Count(plan)==1,"safe sound remains a single deliberate edit"); Commit(f,plan);
   Expect((int)Read(@"EvilIslands\general settings","antialiasing")==99 && ((byte[])Read(@"EvilIslands\general settings","dithering")).SequenceEqual(customDither) && (int)Read(@"EvilIslands\shadow settings","shadowquality")==2 && (int)Read(@"EvilIslands\terrain settings","SHADOWINGFREQ")==1234 && (int)Read(@"EvilIslands\general settings","fullscreen")==1 && ((byte[])Read(@"EvilIslands\general settings","mipmapping")).SequenceEqual(customMips),"saving sound preserves all six obsolete and custom registry values");
   Expect((int)Read(@"EvilIslands\general settings","FPSIndependentCursor")==1 && (int)Read(@"EvilIslands\general settings","TextureQuality")==99 && ((byte[])Read(@"EvilIslands\general settings","filtering")).SequenceEqual(customFilter) && (string)Read(@"EvilIslands\general settings","video")=="custom movies" && (int)Read(@"EvilIslands\terrain settings","EnableWaterWaves")==1 && ((byte[])Read(@"EvilIslands\terrain settings","LOD1")).SequenceEqual(customLod1) && ((byte[])Read(@"EvilIslands\terrain settings","LOD2")).SequenceEqual(customLod2) && (int)Read(@"EvilIslands\terrain settings","LIGHTINGFREQ")==1234 && (int)Read("settings","ObjectsDrawRadius")==10 && (int)Read("settings","LandscapeDrawRadius")==5000,"saving sound preserves all ten newly retired values and types");
  }
  Reset(); Write("settings","MP console",1);
  using(var f=Open()){
   ((CheckBox)Field(f,"cbMPConsoleSupport")).Checked=false; var plan=Plan(f); Commit(f,plan);
  }
  using(var f=Open()){Expect(!((CheckBox)Field(f,"cbMPConsoleSupport")).Checked && Count(Plan(f))==0,"saved flag survives reopen without extra writes");}
  Reset();
  using(var f=Open()){
   ((ComboBox)Field(f,"cbbAudio")).SelectedIndex=2;
   ((ComboBox)Field(f,"cbbResolutions")).Items.Add("not a resolution");
   ((ComboBox)Field(f,"cbbResolutions")).SelectedIndex=((ComboBox)Field(f,"cbbResolutions")).Items.Count-1;
   Throws(()=>Plan(f),"invalid resolution rejects unrelated pending sound edit");
   Expect(Read(@"EvilIslands\general settings","audio")==null,"invalid resolution writes nothing");
  }
  Reset();
  using(var f=Open()){
   ((CheckBox)Field(f,"cbMPConsoleSupport")).CheckState=CheckState.Checked;
  }
  Expect(Read("settings","MP console")==null,"closing without save writes nothing");
  Reset(); Write(@"EvilIslands\camera settings","FarClipDistance",BitConverter.GetBytes(float.PositiveInfinity));
  using(var f=Open()){
   var m=((PropertyGrid)Field(f,"propGridCamera")).SelectedObject;
   m.GetType().GetProperty("NearClipDistance").SetValue(m,1f,null);
   Throws(()=>Plan(f),"changed clip requires finite existing paired limit");
  }
  Reset();
  using(var f=Open()){
   Call(f,"BuildSettings");
   ((CheckBox)Field(f,"cbMPConsoleSupport")).CheckState=CheckState.Checked;
   Call(f,"SaveSettings",false);
   Expect(f.DialogResult==DialogResult.OK && (int)Read("settings","MP console")==1,"dialog save handler commits and then returns OK");
  }
  Console.WriteLine("TOTAL "+passed+" PASS; only disposable validation registry root was used.");return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}finally{Reset();}
 }
 static IEnumerable<Control> Walk(Control c){foreach(Control x in c.Controls){yield return x;foreach(var y in Walk(x))yield return y;}}
}
