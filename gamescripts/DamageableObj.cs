using Godot;

public static class dmgModDefaultData{
  private static DamageableObj.dmgObjProperties.dmgMod _dmgMod_One = new DamageableObj.dmgObjProperties.dmgMod{
    NormalModifier = 1f,
    AcidModifier = 1f,
    ToxicModifier = 1f,
    BurnModifier = 1f,
    ElectrocuteModifier = 1f,
    StunChance = 1f
  };

  private static DamageableObj.dmgObjProperties.dmgMod _dmgMod_Human = new DamageableObj.dmgObjProperties.dmgMod{
    NormalModifier = 1f,
    AcidModifier = 0.2f,
    ToxicModifier = 1f,
    BurnModifier = 1f,
    ElectrocuteModifier = 1.8f,
    StunChance = 0.4f
  };

  private static DamageableObj.dmgObjProperties.dmgMod _dmgMod_Robot = new DamageableObj.dmgObjProperties.dmgMod{
    NormalModifier = 1.2f,
    AcidModifier = 0.8f,
    ToxicModifier = 0f,
    BurnModifier = 1.6f,
    ElectrocuteModifier = 3.2f,
    StunChance = 0.8f
  };

  public static DamageableObj.dmgObjProperties.dmgMod dmgMod_Human{
    get{
      return _dmgMod_Human;
    }
  }

  public static DamageableObj.dmgObjProperties.dmgMod dmgMod_Robot{
    get{
      return _dmgMod_Robot;
    }
  }

  public static DamageableObj.dmgObjProperties.dmgMod dmgMod_One{
    get{
      return _dmgMod_One;
    }
  }
}

public class DamageableObj: RigidBody2D{
  public struct dmgObjProperties{
    public struct dmgMod{
      public float NormalModifier, AcidModifier, ToxicModifier, BurnModifier, ElectrocuteModifier;
      public float StunChance;
    }
    
    public enum ObjType{
      biological,
      metal,
      
      // other obj type will make all damage type become normal, except burn
      other
    }
    
    public dmgMod modifier;
    public ObjType Otype;
  }

  [Signal]
  private delegate void _OnHealthDepleted();
  [Signal]
  // health as float will be passed
  private delegate void _OnHealthChanged();


  /*  elemental damage functionality variables  */
  // for toxic damage
  private Timer toxicDamageLastTimer = new Timer();
  private bool doToxicDamage = false;
  private float currentdamage_toxic;
  private float distanceEveryDamage, currentDistance;

  // for burn damage
  private Timer burnDamageLastTimer = new Timer();
  private Timer burnDamageStepTimer = new Timer();
  private float currentdamage_burn;

  // for electrocute damage
  private bool isStunned = false;
  private Timer StunnedTimer = new Timer();

  // for acid damage
  private Timer acidDamageLastTimer = new Timer();
  private Timer acidDamageStepTimer = new Timer();
  private float currentdamage_acid;



  protected int obj_maxhealth = 0;
  
  [Export]
  protected int obj_actualmaxhealth = 10;

  protected dmgObjProperties obj_dmgproperties;

  protected int maxHealth{
    get{return obj_maxhealth;}
  }
  
  protected int actualMaxHealth{
    get{return obj_actualmaxhealth;}
  }

  protected float CurrentHealth{
    get{
      return obj_currenthealth;
    }
  }

  private float obj_currenthealth;


  private void checkHealth(){
    EmitSignal("_OnHealthChanged", obj_currenthealth);
    if(obj_currenthealth <= 0)
      EmitSignal("_OnHealthDepleted");
  }


  // handling timer based elemental damages

  private void OnToxicDamageFinished(){
    doToxicDamage = false;
  }

  private void OnBurnDamageFinished(){
    burnDamageStepTimer.Stop();
  }

  private void OnBurnDamageStep(){
    obj_currenthealth -= currentdamage_burn * obj_dmgproperties.modifier.BurnModifier;
    checkHealth();
  }

  private void OnAcidDamageStep(){
    float reduce = currentdamage_acid * obj_dmgproperties.modifier.AcidModifier;
    obj_currenthealth -= Mathf.RoundToInt(reduce);

    if(obj_dmgproperties.Otype == dmgObjProperties.ObjType.metal)
      obj_maxhealth -= Mathf.CeilToInt(reduce * 0.05f);

    checkHealth();
  }


  private void OnHealthDepleted(){
    // burn damage
    burnDamageStepTimer.Stop();
    burnDamageLastTimer.Stop();

    // toxic damage
    doToxicDamage = false;

    // acid damage
    acidDamageStepTimer.Stop();
    acidDamageLastTimer.Stop();

    // stun
    StunnedTimer.Stop();
  }

  // this need to be suplied just in case if the class needed it for toxic damage
  private void registerMovement(float len){
    if(doToxicDamage){
      currentDistance += len;
      if(currentDistance >= distanceEveryDamage){
        currentDistance -= distanceEveryDamage;
        obj_currenthealth -= currentdamage_toxic * obj_dmgproperties.modifier.ToxicModifier;
        checkHealth();
      }
    }
  }

  private void OnStunFinished(){
    isStunned = false;
  }

  private void Stunned(float time){
    isStunned = true;
    StunnedTimer.Start();
  }


  protected void changeMaxHealth(int health, bool refillHealth = false){
    obj_maxhealth = health;
    if(refillHealth)
      obj_currenthealth = (float)obj_maxhealth;
  }


  /*  elemental damage handling functions  */
  // can be reuseable for healing
  protected void doDamage(float dmg){
    obj_currenthealth -= dmg * obj_dmgproperties.modifier.NormalModifier;
    checkHealth();
  }
  
  protected void doDamage_Toxic(ref damagedata dd){
    switch(obj_dmgproperties.Otype){
      case dmgObjProperties.ObjType.biological:
        if(dd.doNormalDamage)
          doDamage(dd.damage);
          
        currentdamage_toxic = dd.elementalDmg;
        distanceEveryDamage = dd.step;
        toxicDamageLastTimer.Start(dd.lasttime);
        doToxicDamage = true;
        currentDistance = distanceEveryDamage;
        
      break;

      
      case dmgObjProperties.ObjType.metal:
      case dmgObjProperties.ObjType.other:
        doDamage(dd.damage);
      
      break;
    }
  }

  protected void doDamage_Burn(ref damagedata dd){
    if(dd.doNormalDamage)
      doDamage(dd.damage);

    currentdamage_burn = dd.elementalDmg;
    burnDamageLastTimer.Start(dd.lasttime);
    burnDamageStepTimer.Start(dd.step);
  }

  protected void doDamage_Electrocute(ref damagedata dd){
    if(dd.doNormalDamage)
      doDamage(dd.damage);

    obj_currenthealth -= dd.elementalDmg;
    if(GD.Randf() <= obj_dmgproperties.modifier.StunChance)
      Stunned(dd.lasttime);
  }

  protected void doDamage_Acid(ref damagedata dd){
    if(dd.doNormalDamage)
      doDamage(dd.damage);
    
    currentdamage_acid = dd.elementalDmg;
    acidDamageLastTimer.Start(dd.lasttime);
    acidDamageStepTimer.Start(dd.step);
  }

  protected void Move(Vector2 Vel){
    Physics2DDirectBodyState bodyState = Physics2DServer.BodyGetDirectState(GetRid());
    bodyState.LinearVelocity = Vel;
    _IntegrateForces(bodyState);
  }


  public override void _Ready(){
    obj_maxhealth = obj_actualmaxhealth;
    obj_currenthealth = (float)obj_maxhealth;

    Connect("_OnHealthDepleted", this, "OnHealthDepleted");

    AddChild(toxicDamageLastTimer);
    toxicDamageLastTimer.Connect("timeout", this, "OnToxicDamageFinished");
    toxicDamageLastTimer.OneShot = true;

    AddChild(burnDamageLastTimer);
    burnDamageLastTimer.Connect("timeout", this, "OnBurnDamageFinished");
    burnDamageLastTimer.OneShot = true;

    AddChild(burnDamageStepTimer);
    burnDamageStepTimer.Connect("timeout", this, "OnBurnDamageStep");
    burnDamageStepTimer.OneShot = false;

    AddChild(StunnedTimer);
    StunnedTimer.Connect("timeout", this, "OnStunFinished");
    StunnedTimer.OneShot = true;
  }

  
  private Vector2 lastpos = Vector2.Up;
  public override void _PhysicsProcess(float delta){
    base._PhysicsProcess(delta);

    if(GlobalPosition != lastpos){
      float dist = (GlobalPosition - lastpos).Length();
      registerMovement(dist);
      lastpos = GlobalPosition;
    }
  }

  // if the effect dealt still the same, the effect will redo itself
  // uses data struct for the info of the damage
  public void DoDamageToObj(ref damagedata dd){
    if(dd.dmgtype == damagedata.damagetype.normal || dd.doNormalDamage)
      doDamage(dd.damage);

    switch(dd.dmgtype){
      // damaging when moving
      case damagedata.damagetype.toxic:
        doDamage_Toxic(ref dd);
        break;

      // lasting damage
      case damagedata.damagetype.burn:
        doDamage_Burn(ref dd);
        break;
      
      // like normal, but it can stun people or destroy robot faster
      case damagedata.damagetype.eletrocute:
        doDamage_Electrocute(ref dd);
        break;
    }
  }
}
