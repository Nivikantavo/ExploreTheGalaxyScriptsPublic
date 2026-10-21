using Playgama;
using UnityEngine;
using Zenject;

public class InputInstaller : MonoInstaller
{
    [SerializeField] private Joystick _joystick;

#if UNITY_EDITOR
    [SerializeField] private bool _testMobileInput;
#endif

    public override void InstallBindings()
    {
#if UNITY_EDITOR
        if(_testMobileInput)
        {
            Container.Bind<IInput>().To<Mobileinput>().FromNew().AsSingle().WithArguments(_joystick).NonLazy();
        }
        else
#endif
        if(Bridge.device.type != Playgama.Modules.Device.DeviceType.Desktop)
        {
            Container.Bind<IInput>().To<Mobileinput>().FromNew().AsSingle().WithArguments(_joystick).NonLazy();
        }
        else
        {
            PlayerInput input = new PlayerInput();
            Container.Bind<IInput>().To<DesctopInput>().FromNew().AsSingle().WithArguments(input).NonLazy();
        }
    }
}
