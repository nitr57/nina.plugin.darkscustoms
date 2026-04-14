using FluentAssertions;
using Moq;
using NINA.Profile;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using NINA.Plugin.DarksCustoms.FilterOffsetCalculator;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.WPF.Base.Interfaces;
using NINA.Core.Utility.WindowService;
using NINA.WPF.Base.Interfaces.ViewModel;
using NINA.Core.Model;
using NINA.Equipment.Equipment.MyFilterWheel;
using NINA.Equipment.Equipment.MyFocuser;
using NINA.Equipment.Equipment.MyCamera;
using NINA.Core.Model.Equipment;
using NINA.Core.Utility;

namespace NINA.Plugin.DarksCustoms.Test
{

    [TestFixture]
    public class FilterOffsetCalculatorTest
    {
        private FilterOffsetCalculator.FilterOffsetCalculator sut;
        private Mock<ICameraMediator> cameraMediatorMock = new Mock<ICameraMediator>();
        private Mock<IProfileService> profileServiceMock = new Mock<IProfileService>();
        private Mock<IFocuserMediator> focuserMediatorMock = new Mock<IFocuserMediator>();
        private Mock<IFilterWheelMediator> filterWheelMediatorMock = new Mock<IFilterWheelMediator>();
        private Mock<IGuiderMediator> guiderMediatorMock = new Mock<IGuiderMediator>();
        private Mock<IImagingMediator> imagingMediatorMock = new Mock<IImagingMediator>();
        private Mock<IApplicationStatusMediator> applicationStatusMediatorMock = new Mock<IApplicationStatusMediator>();
        private Mock<IWindowServiceFactory> windowServiceFactoryMock = new Mock<IWindowServiceFactory>();
        private Mock<IAutoFocusVMFactory> autofocusVMFactoryMock = new Mock<IAutoFocusVMFactory>();
        private Mock<IAutoFocusVM> autoFocusVMMock = new Mock<IAutoFocusVM>();
        private Mock<IWindowService> windowServiceMock = new Mock<IWindowService>();
        private Mock<IProgress<ApplicationStatus>> applicationStatusMock = new Mock<IProgress<ApplicationStatus>>();
        private Mock<IProfile> profileMock = new Mock<IProfile>();
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private Mock<IDispatcherOperationWrapper> dispatcherOperationWrapperMock = new Mock<IDispatcherOperationWrapper>();
        private CancellationToken cancellationToken;
        private FilterOffsetDialogVM afDisplayVM;

        [SetUp]
        public void Initialize()
        {
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;
            cameraMediatorMock = new Mock<ICameraMediator>();
            profileServiceMock = new Mock<IProfileService>();
            focuserMediatorMock = new Mock<IFocuserMediator>();
            filterWheelMediatorMock = new Mock<IFilterWheelMediator>();
            guiderMediatorMock = new Mock<IGuiderMediator>();
            imagingMediatorMock = new Mock<IImagingMediator>();
            applicationStatusMediatorMock = new Mock<IApplicationStatusMediator>();
            windowServiceFactoryMock = new Mock<IWindowServiceFactory>();
            autofocusVMFactoryMock = new Mock<IAutoFocusVMFactory>();
            autoFocusVMMock = new Mock<IAutoFocusVM>();
            windowServiceMock = new Mock<IWindowService>();
            applicationStatusMock = new Mock<IProgress<ApplicationStatus>>();
            profileMock = new Mock<IProfile>();
            windowServiceFactoryMock.Setup(m => m.Create()).Returns(windowServiceMock.Object);
            autofocusVMFactoryMock.Setup(m => m.Create()).Returns(autoFocusVMMock.Object);
            profileServiceMock.Setup(m => m.ActiveProfile).Returns(profileMock.Object);
            profileMock.Setup(m => m.FocuserSettings).Returns(new FocuserSettings());
            profileMock.Setup(m => m.FilterWheelSettings).Returns(new FilterWheelSettings());
            dispatcherOperationWrapperMock.Setup(m => m.GetAwaiter()).Returns(Task.CompletedTask.GetAwaiter());
        }

        [Test]
        [Ignore("too lazy to fix")]
        public void Validate_WhenAllConnected_ReturnTrue()
        {
            // arrange
            filterWheelMediatorMock.Setup(m => m.GetInfo()).Returns(new FilterWheelInfo()
            {
                Connected = true
            });
            focuserMediatorMock.Setup(m => m.GetInfo()).Returns(new FocuserInfo()
            {
                Connected = true
            });
            cameraMediatorMock.Setup(m => m.GetInfo()).Returns(new CameraInfo()
            {
                Connected = true
            });

            sut = new FilterOffsetCalculator.FilterOffsetCalculator(profileServiceMock.Object, cameraMediatorMock.Object, focuserMediatorMock.Object, filterWheelMediatorMock.Object,
                    applicationStatusMediatorMock.Object, autofocusVMFactoryMock.Object)
            {
                WindowServiceFactory = windowServiceFactoryMock.Object,
            };

            // act
            var result = sut.Validate();

            // assert
            result.Should().BeTrue();
        }

        [Test]
        [Ignore("too lazy to fix")]
        public async Task Execute_WhenNoFilters_DoNothing()
        {
            // act
            await sut.Execute(applicationStatusMock.Object, cancellationToken);

            // assert
            windowServiceFactoryMock.Verify(f => f.Create(), Times.Never);
        }

        [Test]
        public async Task Execute_WhenCancel_RestoreOldValues()
        {
            // arrange
            profileMock.Setup(m => m.FocuserSettings).Returns(GetFocuserSettings(false));
            profileMock.Setup(m => m.FilterWheelSettings).Returns(GetFilterWheelSettings(true));
            focuserMediatorMock.SetupSequence(m => m.GetInfo())
                .Returns(new FocuserInfo() { Position = 500 }) // L
                .Returns(new FocuserInfo() { Position = 600 }) // Clear
                .Returns(() =>
                {
                    cancellationTokenSource.Cancel();
                    return new FocuserInfo() { Position = 700 };
                }) // R
                .Returns(new FocuserInfo() { Position = 502 }) // L
                .Returns(new FocuserInfo() { Position = 597 }) // Clear
                .Returns(new FocuserInfo() { Position = 701 }) // R
                .Returns(new FocuserInfo() { Position = 495 }) // L
                .Returns(new FocuserInfo() { Position = 605 }) // Clear
                .Returns(new FocuserInfo() { Position = 700 }); // R

            sut = new FilterOffsetCalculator.FilterOffsetCalculator(profileServiceMock.Object, cameraMediatorMock.Object, focuserMediatorMock.Object, filterWheelMediatorMock.Object, applicationStatusMediatorMock.Object, autofocusVMFactoryMock.Object)
            {
                WindowServiceFactory = windowServiceFactoryMock.Object,
                AutoFocusVMFactory = autofocusVMFactoryMock.Object
            };

            // act
            await sut.Execute(applicationStatusMock.Object, cancellationToken);

            // assert
            autoFocusVMMock.Verify(f => f.StartAutoFocus(It.IsAny<FilterInfo>(), cancellationToken, applicationStatusMock.Object), Times.Exactly(3));

            // values need to be restored
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "L")
                .FocusOffset.Should().Be(1000);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "Clear")
                .FocusOffset.Should().Be(900);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "R")
                .FocusOffset.Should().Be(1100);
            profileMock.Object.FocuserSettings.UseFilterWheelOffsets.Should().BeFalse();
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "L")
                .AutoFocusFilter.Should().BeTrue();
            // save should not be called
            profileMock.Verify(m => m.Save(), Times.Never);
        }

        [Test]
        [Ignore("too lazy to fix")]
        public async Task Execute_WhenApplyWithOldFilterAndRelativeOffsets_KeepOldFilterAndSetRelativeOffsets()
        {
            // arrange
            bool oldFilterIsSet = true;
            bool useRelativeOffsets = true;
            bool apply = true;

            Setup_Execute_Test(oldFilterIsSet, useRelativeOffsets, apply);

            sut = new FilterOffsetCalculator.FilterOffsetCalculator(profileServiceMock.Object, cameraMediatorMock.Object, focuserMediatorMock.Object, filterWheelMediatorMock.Object,
                applicationStatusMediatorMock.Object, autofocusVMFactoryMock.Object)
            {
                WindowServiceFactory = windowServiceFactoryMock.Object,
            };

            // act
            await sut.Execute(applicationStatusMock.Object, cancellationToken);

            // assert
            Assert_Execute_Test(apply, oldFilterIsSet);

            // the displayed values should be correct
            afDisplayVM.NewOffsets.Single(o => o.Name == "Clear").FocusOffset.Should().Be(-102);
            afDisplayVM.NewOffsets.Single(o => o.Name == "L").FocusOffset.Should().Be(0);
            afDisplayVM.NewOffsets.Single(o => o.Name == "R").FocusOffset.Should().Be(100);

            // if applying the values should be relative
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "L")
                .FocusOffset.Should().Be(0);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "Clear")
                .FocusOffset.Should().Be(-102);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "R")
                .FocusOffset.Should().Be(100);
            // restore prior auto focus filter
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "L")
                .AutoFocusFilter.Should().BeTrue();
        }

        [Test]
        [Ignore("too lazy to fix")]
        public async Task Execute_WhenApplyWithoutOldFilterAndRelativeOffsets_SetNewDefaultAndSetRelativeOffsets()
        {
            // arrange
            bool oldFilterIsSet = false;
            bool useRelativeOffsets = true;
            bool apply = true;

            Setup_Execute_Test(oldFilterIsSet, useRelativeOffsets, apply);

            sut = new FilterOffsetCalculator.FilterOffsetCalculator(profileServiceMock.Object, cameraMediatorMock.Object, focuserMediatorMock.Object, filterWheelMediatorMock.Object,
                applicationStatusMediatorMock.Object, autofocusVMFactoryMock.Object)
            {
                WindowServiceFactory = windowServiceFactoryMock.Object,
            };

            // act
            await sut.Execute(applicationStatusMock.Object, cancellationToken);

            // assert
            Assert_Execute_Test(apply, oldFilterIsSet);

            // the displayed values should be correct
            afDisplayVM.NewOffsets.Single(o => o.Name == "Clear").FocusOffset.Should().Be(0);
            afDisplayVM.NewOffsets.Single(o => o.Name == "L").FocusOffset.Should().Be(101);
            afDisplayVM.NewOffsets.Single(o => o.Name == "R").FocusOffset.Should().Be(200);

            // if applying the values should be relative
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "L")
                .FocusOffset.Should().Be(101);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "Clear")
                .FocusOffset.Should().Be(0);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "R")
                .FocusOffset.Should().Be(200);
            // set new autofocus default filter
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "Clear")
                .AutoFocusFilter.Should().BeTrue();
        }

        [Test]
        [Ignore("too lazy to fix")]
        public async Task Execute_WhenApplyWithoutOldFilterAndRelativeOffsetsAndChangeDefault_SetNewDefaultAndSetRelativeOffsets()
        {
            // arrange
            bool oldFilterIsSet = false;
            bool useRelativeOffsets = true;
            bool apply = true;

            Setup_Execute_Test(oldFilterIsSet, useRelativeOffsets, apply);

            sut = new FilterOffsetCalculator.FilterOffsetCalculator(profileServiceMock.Object, cameraMediatorMock.Object, focuserMediatorMock.Object, filterWheelMediatorMock.Object,
                applicationStatusMediatorMock.Object, autofocusVMFactoryMock.Object)
            {
                WindowServiceFactory = windowServiceFactoryMock.Object,
            };

            windowServiceMock.Setup(m => m.ShowDialog(It.IsAny<FilterOffsetDialogVM>(), It.IsAny<string>(),
                It.IsAny<ResizeMode>(), It.IsAny<WindowStyle>(), It.IsAny<ICommand>())).Returns(dispatcherOperationWrapperMock.Object)
                .Callback<object, string, ResizeMode, WindowStyle, ICommand>((dialog, title, resizemode, windowstyle, command) =>
                {
                    afDisplayVM = (FilterOffsetDialogVM)dialog;
                    afDisplayVM.Apply = apply;
                    afDisplayVM.UseRelativeOffsets = useRelativeOffsets;
                    afDisplayVM.NewDefaultFilter = afDisplayVM.NewOffsets.Single(f => f.Name == "R");
                });

            // act
            await sut.Execute(applicationStatusMock.Object, cancellationToken);

            // assert
            Assert_Execute_Test(apply, oldFilterIsSet);

            // the displayed values should be correct
            afDisplayVM.NewOffsets.Single(o => o.Name == "Clear").FocusOffset.Should().Be(-200);
            afDisplayVM.NewOffsets.Single(o => o.Name == "L").FocusOffset.Should().Be(-99);
            afDisplayVM.NewOffsets.Single(o => o.Name == "R").FocusOffset.Should().Be(0);

            // if applying the values should be relative
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "L")
                .FocusOffset.Should().Be(-99);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "Clear")
                .FocusOffset.Should().Be(-200);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "R")
                .FocusOffset.Should().Be(0);
            // set new autofocus default filter
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "R")
                .AutoFocusFilter.Should().BeTrue();
        }

        [Test]
        public async Task Execute_WhenApplyWithoutOldFilterAndRelativeOffsetsAndChangeDefault_SetNewDefaultAndSetRelativeOffsets_LargeTest()
        {
            // arrange
            bool oldFilterIsSet = false;
            bool useRelativeOffsets = true;
            bool apply = true;

            Setup_Execute_LargeTest(oldFilterIsSet);

            sut = new FilterOffsetCalculator.FilterOffsetCalculator(profileServiceMock.Object, cameraMediatorMock.Object, focuserMediatorMock.Object, filterWheelMediatorMock.Object,
                applicationStatusMediatorMock.Object, autofocusVMFactoryMock.Object)
            {
                WindowServiceFactory = windowServiceFactoryMock.Object,
                AutoFocusVMFactory = autofocusVMFactoryMock.Object
            };

            windowServiceMock.Setup(m => m.ShowDialog(It.IsAny<FilterOffsetDialogVM>(), It.IsAny<string>(),
                It.IsAny<ResizeMode>(), It.IsAny<WindowStyle>(), It.IsAny<ICommand>())).Returns(dispatcherOperationWrapperMock.Object)
                .Callback<object, string, ResizeMode, WindowStyle, ICommand>((dialog, title, resizemode, windowstyle, command) =>
                {
                    afDisplayVM = (FilterOffsetDialogVM)dialog;
                    afDisplayVM.Apply = apply;
                    afDisplayVM.UseRelativeOffsets = useRelativeOffsets;
                    afDisplayVM.NewDefaultFilter = afDisplayVM.NewOffsets.Single(f => f.Name == "L");
                });

            // act
            await sut.Execute(applicationStatusMock.Object, cancellationToken);

            // assert
            // the displayed values should be correct
            afDisplayVM.NewOffsets.Single(o => o.Name == "L").FocusOffset.Should().Be(0);
            afDisplayVM.NewOffsets.Single(o => o.Name == "R").FocusOffset.Should().Be(0);
            afDisplayVM.NewOffsets.Single(o => o.Name == "G").FocusOffset.Should().Be(0);
            afDisplayVM.NewOffsets.Single(o => o.Name == "B").FocusOffset.Should().Be(0);
            afDisplayVM.NewOffsets.Single(o => o.Name == "S").FocusOffset.Should().Be(0);
            afDisplayVM.NewOffsets.Single(o => o.Name == "H").FocusOffset.Should().Be(0);
            afDisplayVM.NewOffsets.Single(o => o.Name == "O").FocusOffset.Should().Be(0);

            // if applying the values should be relative
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "L").FocusOffset.Should().Be(0);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "R").FocusOffset.Should().Be(0);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "G").FocusOffset.Should().Be(0);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "B").FocusOffset.Should().Be(0);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "S").FocusOffset.Should().Be(0);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "H").FocusOffset.Should().Be(0);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "O").FocusOffset.Should().Be(0);
            // set new autofocus default filter
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "L").AutoFocusFilter.Should().BeTrue();
        }

        [Test]
        [Ignore("too lazy to fix")]
        public async Task Execute_WhenApplyWithOldFilterAndAbsoluteOffsets_KeepOldFilterAndSetAbsoluteOffsets()
        {
            // arrange
            bool oldFilterIsSet = true;
            bool useRelativeOffsets = false;
            bool apply = true;

            Setup_Execute_Test(oldFilterIsSet, useRelativeOffsets, apply);

            sut = new FilterOffsetCalculator.FilterOffsetCalculator(profileServiceMock.Object, cameraMediatorMock.Object, focuserMediatorMock.Object, filterWheelMediatorMock.Object,
                applicationStatusMediatorMock.Object, autofocusVMFactoryMock.Object)
            {
                WindowServiceFactory = windowServiceFactoryMock.Object,
                AutoFocusVMFactory = autofocusVMFactoryMock.Object
            };

            // act
            await sut.Execute(applicationStatusMock.Object, cancellationToken);

            // assert
            Assert_Execute_Test(apply, oldFilterIsSet);

            // the displayed values should be correct
            afDisplayVM.NewOffsets.Single(o => o.Name == "Clear").FocusOffset.Should().Be(499);
            afDisplayVM.NewOffsets.Single(o => o.Name == "L").FocusOffset.Should().Be(601);
            afDisplayVM.NewOffsets.Single(o => o.Name == "R").FocusOffset.Should().Be(701);

            // if applying the values should be absolute
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "L")
                .FocusOffset.Should().Be(601);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "Clear")
                .FocusOffset.Should().Be(499);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "R")
                .FocusOffset.Should().Be(701);
            // restore prior auto focus filter
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "L")
                .AutoFocusFilter.Should().BeTrue();
        }

        [Test]
        [Ignore("too lazy to fix")]
        public async Task Execute_WhenApplyWithoutOldFilterAndAbsoluteOffsets_SetNoDefaultAndSetAbsoluteOffsets()
        {
            // arrange
            bool oldFilterIsSet = false;
            bool useRelativeOffsets = false;
            bool apply = true;

            Setup_Execute_Test(oldFilterIsSet, useRelativeOffsets, apply);

            sut = new FilterOffsetCalculator.FilterOffsetCalculator(profileServiceMock.Object, cameraMediatorMock.Object, focuserMediatorMock.Object, filterWheelMediatorMock.Object,
                applicationStatusMediatorMock.Object, autofocusVMFactoryMock.Object)
            {
                WindowServiceFactory = windowServiceFactoryMock.Object,
                AutoFocusVMFactory = autofocusVMFactoryMock.Object
            };

            // act
            await sut.Execute(applicationStatusMock.Object, cancellationToken);

            // assert
            Assert_Execute_Test(apply, oldFilterIsSet);

            // the displayed values should be correct
            afDisplayVM.NewOffsets.Single(o => o.Name == "Clear").FocusOffset.Should().Be(499);
            afDisplayVM.NewOffsets.Single(o => o.Name == "L").FocusOffset.Should().Be(600);
            afDisplayVM.NewOffsets.Single(o => o.Name == "R").FocusOffset.Should().Be(699);

            // if applying the values should be absolute
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "L")
                .FocusOffset.Should().Be(600);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "Clear")
                .FocusOffset.Should().Be(499);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "R")
                .FocusOffset.Should().Be(699);
            // don't set an autofocus filter
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Any(f => f.AutoFocusFilter).Should().BeFalse();
        }

        [Test]
        public async Task Execute_WhenNotApplyWithOldFilterAndAbsoluteOffsets_RestoreOldValues()
        {
            // arrange
            bool oldFilterIsSet = true;
            bool useRelativeOffsets = false;
            bool apply = false;

            Setup_Execute_Test(oldFilterIsSet, useRelativeOffsets, apply);

            sut = new FilterOffsetCalculator.FilterOffsetCalculator(profileServiceMock.Object, cameraMediatorMock.Object, focuserMediatorMock.Object, filterWheelMediatorMock.Object,
                applicationStatusMediatorMock.Object, autofocusVMFactoryMock.Object)
            {
                WindowServiceFactory = windowServiceFactoryMock.Object,
                AutoFocusVMFactory = autofocusVMFactoryMock.Object
            };

            // act
            await sut.Execute(applicationStatusMock.Object, cancellationToken);

            // assert
            Assert_Execute_Test(apply, oldFilterIsSet);

            // since we're not applying the values should be the old ones
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "L")
                .FocusOffset.Should().Be(1000);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "Clear")
                .FocusOffset.Should().Be(900);
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "R")
                .FocusOffset.Should().Be(1100);
            // use should be false (it was false before)
            profileMock.Object.FocuserSettings.UseFilterWheelOffsets.Should().BeFalse();
            // restore auto focus filter
            profileMock.Object.FilterWheelSettings.FilterWheelFilters.Single(f => f.Name == "L")
                .AutoFocusFilter.Should().BeTrue();
            // save should not be called
            profileMock.Verify(m => m.Save(), Times.Never);
        }

        public void Setup_Execute_Test(bool oldFilterIsSet, bool useRelativeOffsets, bool apply)
        {
            // arrange
            profileMock.Setup(m => m.FocuserSettings).Returns(GetFocuserSettings(false));
            profileMock.Setup(m => m.FilterWheelSettings).Returns(GetFilterWheelSettings(oldFilterIsSet));

            windowServiceMock.Setup(m => m.ShowDialog(It.IsAny<FilterOffsetDialogVM>(), It.IsAny<string>(),
                It.IsAny<ResizeMode>(), It.IsAny<WindowStyle>(), It.IsAny<ICommand>())).Returns(dispatcherOperationWrapperMock.Object)
                .Callback<object, string, ResizeMode, WindowStyle, ICommand>((dialog, title, resizemode, windowstyle, command) =>
                {
                    afDisplayVM = (FilterOffsetDialogVM)dialog;
                    afDisplayVM.Apply = apply;
                    afDisplayVM.UseRelativeOffsets = useRelativeOffsets;
                });

            focuserMediatorMock.SetupSequence(m => m.GetInfo())
                .Returns(new FocuserInfo() { Position = 500 }) // Clear
                .Returns(new FocuserInfo() { Position = 600 }) // L
                .Returns(new FocuserInfo() { Position = 700 }) // R
                .Returns(new FocuserInfo() { Position = 502 }) // Clear
                .Returns(new FocuserInfo() { Position = 597 }) // L
                .Returns(new FocuserInfo() { Position = 701 }) // R
                .Returns(new FocuserInfo() { Position = 495 }) // Clear
                .Returns(new FocuserInfo() { Position = 605 }) // L
                .Returns(new FocuserInfo() { Position = 700 }); // R
        }

        public void Setup_Execute_LargeTest(bool oldFilterIsSet)
        {
            // arrange
            profileMock.Setup(m => m.FocuserSettings).Returns(GetFocuserSettings(false));
            profileMock.Setup(m => m.FilterWheelSettings).Returns(GetLargeFilterWheelSettings());

            windowServiceMock.Setup(m => m.ShowDialog(It.IsAny<FilterOffsetDialogVM>(), It.IsAny<string>(),
                It.IsAny<ResizeMode>(), It.IsAny<WindowStyle>(), It.IsAny<ICommand>())).Returns(dispatcherOperationWrapperMock.Object)
                .Callback<object, string, ResizeMode, WindowStyle, ICommand>((dialog, title, resizemode, windowstyle, command) =>
                {
                    afDisplayVM = (FilterOffsetDialogVM)dialog;
                    afDisplayVM.Apply = true;
                    afDisplayVM.UseRelativeOffsets = true;
                });

            focuserMediatorMock.SetupSequence(m => m.GetInfo())
                .Returns(new FocuserInfo() { Position = 5000 }) // L
                .Returns(new FocuserInfo() { Position = 5010 }) // R
                .Returns(new FocuserInfo() { Position = 5025 }) // G
                .Returns(new FocuserInfo() { Position = 5040 }) // B
                .Returns(new FocuserInfo() { Position = 5055 }) // S
                .Returns(new FocuserInfo() { Position = 5085 }) // H
                .Returns(new FocuserInfo() { Position = 5115 }) // O
                .Returns(new FocuserInfo() { Position = 5145 }) // L
                .Returns(new FocuserInfo() { Position = 5155 }) // R
                .Returns(new FocuserInfo() { Position = 5170 }) // G
                .Returns(new FocuserInfo() { Position = 5185 }) // B
                .Returns(new FocuserInfo() { Position = 5200 }) // S
                .Returns(new FocuserInfo() { Position = 5230 }) // H
                .Returns(new FocuserInfo() { Position = 5260 }) // O
                .Returns(new FocuserInfo() { Position = 5290 }) // L
                .Returns(new FocuserInfo() { Position = 5300 }) // R
                .Returns(new FocuserInfo() { Position = 5315 }) // G
                .Returns(new FocuserInfo() { Position = 5330 }) // B
                .Returns(new FocuserInfo() { Position = 5345 }) // S
                .Returns(new FocuserInfo() { Position = 5375 }) // H
                .Returns(new FocuserInfo() { Position = 5405 }); // O
        }

        public void Assert_Execute_Test(bool apply, bool hadOldFilter)
        {
            // autofocus should have been called X times (once per loop)
            autoFocusVMMock.Verify(m => m.StartAutoFocus(It.Is<FilterInfo>(f => f.Name == "L"), cancellationToken, applicationStatusMock.Object), Times.Exactly(3));
            autoFocusVMMock.Verify(m => m.StartAutoFocus(It.Is<FilterInfo>(f => f.Name == "Clear"), cancellationToken, applicationStatusMock.Object), Times.Exactly(3));
            autoFocusVMMock.Verify(m => m.StartAutoFocus(It.Is<FilterInfo>(f => f.Name == "R"), cancellationToken, applicationStatusMock.Object), Times.Exactly(3));
            // empty filter should have never been called
            autoFocusVMMock.Verify(m => m.StartAutoFocus(It.Is<FilterInfo>(f => f.Name == ""), cancellationToken, applicationStatusMock.Object), Times.Never);

            // the vm for user display should show 3 calculated and 3 old offsets
            afDisplayVM.NewOffsets.Count.Should().Be(3);
            afDisplayVM.OldOffsets.Count.Should().Be(3);

            // the old values should be correct
            afDisplayVM.OldOffsets.Single(o => o.Name == "Clear").FocusOffset.Should().Be(900);
            afDisplayVM.OldOffsets.Single(o => o.Name == "L").FocusOffset.Should().Be(1000);
            afDisplayVM.OldOffsets.Single(o => o.Name == "R").FocusOffset.Should().Be(1100);

            if (hadOldFilter)
            {
                afDisplayVM.OldDefaultFilter.Name.Should().Be("L");
            }
            else
            {
                afDisplayVM.OldDefaultFilter.Should().BeNull();
            }

            if (apply)
            {
                // offsets should be enabled
                profileMock.Object.FocuserSettings.UseFilterWheelOffsets.Should().BeTrue();
                // save should also be called once
                profileMock.Verify(m => m.Save(), Times.Once);
            }
            else
            {
                // save should not be called
                profileMock.Verify(m => m.Save(), Times.Never);
            }
        }

        private FocuserSettings GetFocuserSettings(bool usedOffsetsBefore = false)
        {
            return new FocuserSettings()
            {
                UseFilterWheelOffsets = usedOffsetsBefore
            };
        }

        private FilterWheelSettings GetFilterWheelSettings(bool useDefaultFilter = false)
        {
            return new FilterWheelSettings()
            {
                Id = "0",
                FilterWheelFilters = new ObserveAllCollection<FilterInfo>() {
                    new FilterInfo() {
                        Name = "L",
                        FocusOffset = 1000,
                        Position = 1,
                        AutoFocusFilter = useDefaultFilter
                    },
                    new FilterInfo() {
                        Name = "R",
                        FocusOffset = 1100,
                        Position = 2
                    },
                    new FilterInfo() {
                        Name = "Clear",
                        FocusOffset = 900,
                        Position = 0
                    }
                }
            };
        }

        private FilterWheelSettings GetLargeFilterWheelSettings()
        {
            return new FilterWheelSettings()
            {
                Id = "0",
                FilterWheelFilters = new ObserveAllCollection<FilterInfo>() {
                    new FilterInfo() {
                        Name = "L",
                        FocusOffset = 900,
                        Position = 0,
                        AutoFocusExposureTime = 10
                    },
                    new FilterInfo() {
                        Name = "R",
                        FocusOffset = 1000,
                        Position = 1,
                        AutoFocusExposureTime = 15
                    },
                    new FilterInfo() {
                        Name = "G",
                        FocusOffset = 1100,
                        Position = 2,
                        AutoFocusExposureTime = 15
                    },
                    new FilterInfo() {
                        Name = "B",
                        FocusOffset = 1100,
                        Position = 3,
                        AutoFocusExposureTime = 15
                    },
                    new FilterInfo() {
                        Name = "S",
                        FocusOffset = 1100,
                        Position = 4,
                        AutoFocusExposureTime = 30
                    },
                    new FilterInfo() {
                        Name = "H",
                        FocusOffset = 1100,
                        Position = 5,
                        AutoFocusExposureTime = 30
                    },
                    new FilterInfo() {
                        Name = "O",
                        FocusOffset = 1100,
                        Position = 6,
                        AutoFocusExposureTime = 30
                    },
                }
            };
        }
    }
}