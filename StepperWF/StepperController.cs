using CommandMessenger;
using CommandMessenger.Transport.Serial;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;

namespace StepperWF
{
    public class StepperController
    {
        public struct CommandStructure
        {
            public string name;
            public string description;
            public string parameters;
            public string returns;
        }
        public bool RunLoop { get; set; }
        private SerialTransport _serialTransport;
        public  CmdMessenger _cmdMessenger;
        private int _receivedItemsCount;                        // Counter of number of plain text items received
        private int _receivedBytesCount;                        // Counter of number of plain text bytes received
        long _beginTime;                                        // Start time, 1st item of sequence received 
        long _endTime;                                          // End time, last item of sequence received 
        public Dictionary<string, int> CommandNumber;
        public  CommandStructure[] commandStructure = new CommandStructure[] {
        new CommandStructure{name="Acknowledge",description=" //0  Command to acknowledge that cmd was received",parameters="",returns="i"},
        new CommandStructure{name="Error",description="1  Command to report errors",parameters="",returns=""},
        new CommandStructure { name = "FloatAddition", description = "2  Command to request add two floats", parameters = "ff" },
        new CommandStructure { name = "FloatAdditionResult", description = "3  Command to report addition result", parameters = "" },
        new CommandStructure { name = "GetFwVerNum", description = "4  Command to get firmware version as a float", parameters = "f" },
        new CommandStructure { name = "GetFwVerStr", description = "5  Command to get firmware version as a string", parameters = "s" },
        new CommandStructure { name = "MoveToPosition", description = "6  Command to move current motor to absolute target position", parameters = "i" ,returns=""},
        new CommandStructure { name = "GetCurrentMotorStatus", description = "7  Command to get current motor status", parameters = "" ,returns="i"},
        new CommandStructure { name = "CurrentMotorStop", description = "8  Command to stop current motor move", parameters = "" ,returns="i"},
        new CommandStructure { name = "AwaitCurrentMotorMoveDone", description = "9  Command to await motor move completion", parameters = "" ,returns="i"},
        new CommandStructure { name = "InitializeAxis", description = "10 Command to init current axis using optical switch", parameters = "" ,returns="fff"},
        new CommandStructure { name = "SetMovePolarity", description = "11 Command to set move polarity; false is clockwise positive, reversed (true) is ccw positive", parameters = "b" ,returns=""},
        new CommandStructure { name = "MoveRelative", description = "12 Command to move motor to relative target position", parameters = "i" ,returns=""},
        new CommandStructure { name = "GetPosition", description = "13 Command to get current motor position", parameters = "" ,returns="i"},
        new CommandStructure { name = "SetMaxTravel", description = "14 Command to set max travel distance", parameters = "l" ,returns=""},
        new CommandStructure { name = "SetMaxSpeed", description = "15 Command to set max motor speed", parameters = "l" ,returns=""},
        new CommandStructure { name = "SetCurrentScaling", description = "16 Command to set motor current scaling (% of max current) -- added in V1.0.1", parameters = "i" ,returns=""},
        new CommandStructure { name = "SetCurrentAxis", description = "17 Command to set current axis (defaults to 1) -- added in V1.0.2", parameters = "i" ,returns=""},
        new CommandStructure { name = "MultiMoveToPosition", description = "18 Command to move multiple motors to absolute target position -- added in V1.0.3", parameters = "iiii" ,returns=""},
        new CommandStructure { name = "MultiMoveRelative", description = "19 Command to move multiple motors to relative target position -- added in V1.0.4", parameters = "iiii" ,returns="i"},
        new CommandStructure { name = "SetAcceleration", description = "20 Command to set acceleration value in steps/sec/sec (default is 50,000)", parameters = "l" ,returns=""},
        new CommandStructure { name = "MoveContinuous", description = "21 Command to rotate until stopped", parameters = "b" ,returns=""},
        new CommandStructure { name = "ResetPosition", description = "22 Command to reset the current motor position to zero", parameters = "" ,returns=""},
        new CommandStructure { name = "SetMaxHomeSearchMove", description = "23 Command to set max search distance for home", parameters = "l" ,returns=""},
        new CommandStructure { name = "SetHomingSpeed", description = "24 Command to set homing speed", parameters = "" ,returns=""},
        new CommandStructure { name = "GetHomeFlagState", description = "25 Command to get home flag state for current axis", parameters = "" ,returns="i"},
        new CommandStructure { name = "GetLastInitPosition", description = "26 Command to get last axis init position", parameters = "" ,returns="i"},
        new CommandStructure { name = "SetMotorEnabledState", description = "27 Command to set flag specifying whether motor is enabled", parameters = "ib" ,returns=""},
        new CommandStructure { name = "LEDsOff", description = "28 Command to set color of all NeoPixels to Off", parameters = "" ,returns=""},
        new CommandStructure { name = "LEDsIdle", description = "29 Command to set color of all NeoPixels to Blue", parameters = "" ,returns=""},
        new CommandStructure { name = "LEDsRun", description = "30 Command to send green Wipe pattern to NeoPixels", parameters = "" ,returns=""},
        new CommandStructure { name = "LEDsError", description = "31 Command to send red Flash pattern to NeoPixels", parameters = "" ,returns=""},
        new CommandStructure { name = "GetADCRawVoltage", description = "32 Command to get ADC value in unscaled units -- added in V1.0.18", parameters = "" ,returns="f"},
        new CommandStructure { name = "SetOutHigh", description = "33 Command to set an output pin high", parameters = "i" ,returns=""},
        new CommandStructure { name = "SetOutLow", description = "34 Command to set an output pin low", parameters = "i" ,returns=""},
        new CommandStructure { name = "SetLimitSwitchPolarity", description = "35 Command to set polarity of switches used; T=default (false if blocked)", parameters = "b" ,returns=""},
        new CommandStructure { name = "SetCurrentLimitSwitch", description = "36 Command to set currently active limit switch (it is auto-set on axis change)", parameters = "i" ,returns=""},
        new CommandStructure { name = "HInitializeXY", description = "37 Command to init X axis of H-Bot using optical switch", parameters = "" ,returns=""},
        new CommandStructure { name = "HMoveRelative", description = "38 Command to move X,Y of H-Bot to relative target position", parameters = "" ,returns=""},
        new CommandStructure { name = "HMoveToPosition", description = "39 Command to move X,Y of H-Bot to absolute target position; 3 params (1st param is 1, 2, 3 for X, Y, Both)", parameters = "ill" ,returns=""},
        new CommandStructure { name = "HGetXY", description = "40 Command to get X,Y coordinate of H-Bot", parameters = "" ,returns="ii"},
        new CommandStructure { name = "GetDebugStr", description = "41 Command to get Debug string", parameters = "" ,returns="s"},
        new CommandStructure { name = "HMoveDoneMssg", description = "42 Message to communicate that an H-move is complete: HInitializeXY, HMoveRelative, HMoveToPosition", parameters = "" ,returns=""},
        new CommandStructure { name = "SetHoldCurrentScaling", description = "43 for compatibility with L6470 firmware", parameters = "" ,returns=""},
        new CommandStructure { name = "SetMicroStepModeL6470", description = "44 for compatibility with L6470 firmware", parameters = "" ,returns=""},
        new CommandStructure { name = "LEDsSetColor", description = "45 Command to set color of each NeoPixel (3)", parameters = "iii" ,returns=""},
        new CommandStructure { name = "EnableEOT", description = "46 Command to enable all EOT sensors to stop motion on change", parameters = "" ,returns=""},
        new CommandStructure { name = "DisableEOT", description = "47 Command to disable all EOT sensors to stop motion on change", parameters = "" ,returns=""},
        new CommandStructure { name = "Set2209StallThresh", description = "48 Command to set TMC2209 stall threshold value", parameters = "i" ,returns=""},
        new CommandStructure { name = "Set2209MotorCurrent", description = "49 Command to set TMC2209 motor current in mA", parameters = "i" ,returns=""},
        new CommandStructure { name = "Set2209MicroStepMode", description = "50 Command to set TMC2209 microstep mode -- param is 2,4,8,16,...,256", parameters = "i" ,returns=""},
        new CommandStructure { name = "StopOn2209Stall", description = "51 Command to set TMC2209 to stop when threshold exceeded -- param T|F", parameters = "b" ,returns=""},
        new CommandStructure { name = "Init2209", description = "52 Command to initialize the TMC2209", parameters = "" ,returns=""},
        new CommandStructure { name = "SensorOverride", description = "53 Command to stop on any of the 8 sensors", parameters = "ib" ,returns=""},
        new CommandStructure { name = "SensorPolarity", description = "54 Command to set polarity of any of the 8 sensors", parameters = "ib" ,returns=""},
        new CommandStructure { name = "GetSensorState", description = "55 Command to get state of any of the 8 sensors", parameters = "i" ,returns="b"},
        new CommandStructure { name = "SetFlexiForceStallThresh", description = "56 Command to set FlexiForce stall threshold value for current motor", parameters = "i" ,returns=""},
        new CommandStructure { name = "StopOnFlexiForceStall", description = "57 Command to set current motor to stop when FlexiForce threshold exceeded -- param T|F", parameters = "b" ,returns=""},
        new CommandStructure { name = "GetFlexiForce", description = "58 Command to get last value of FlexiForce Sensor specified in the param (1 or 2)", parameters = "i" ,returns="i"},
        new CommandStructure { name = "GetSwitchSet", description = "59 Command to get last value of Switch Block specified in the param (1 or 2 -- 1=microswitches, 2=optoswitches)", parameters = "i" ,returns="u"},
        new CommandStructure { name = "StreamFlexiForceDebug", description = "60 Command to stream FlexiForce debug data -- param T|F", parameters = "b" ,returns="s"},
        new CommandStructure { name = "StreamSwitchSetDebug", description = "61 Command to stream Switch settings (debug); Switch Block specified in the param (1 or 2 -- 1=microswitches, 2=optoswitches)", parameters = "" ,returns=""},
        new CommandStructure { name = "CallbacksForSwitchChanges", description = "62 Command to enable or disable callbacks on Switch Changes", parameters = "" ,returns=""},
        new CommandStructure { name = "LEDsSetPattern", description = "63 Command to set color and pattern of each NeoPixel (3) -- added V1.26", parameters = "iii" ,returns=""},
        new CommandStructure { name = "LEDsSetBrightness", description = "64 Command to set brightness of NeoPixels (3) -- added V1.26", parameters = "i" ,returns=""},
        new CommandStructure { name = "GetReflectiveSensorValue", description = "65 Command to get last value of Reflective Sensor specified in the param (1 or 2)", parameters = "i" ,returns="i"},
        new CommandStructure { name = "StreamReflectiveValues", description = "66 Command to stream Reflective debug data -- param T|F", parameters = "b" ,returns="s"},
        new CommandStructure { name = "GetLightSensorValue", description = "67 Command to get last value of Light Sensor specified in the param (1 or 2)", parameters = "i" ,returns="i"},
        new CommandStructure { name = "StreamLightValues", description = "68 Command to stream Light debug data -- param T|F", parameters = "b" ,returns="s"}
    };
        public SerialTransport serialPort;
        public MacroRunner macroRunner;
        public string CurrentMacro;
        public StepperController()
        {
            Setup();
            macroRunner = new MacroRunner( this, CurrentMacro, _serialTransport );
            int i = 0;
            foreach(CommandStructure command in commandStructure)
            {
                CommandNumber.Add( command.name, i++ );
            }

        }
        public void Setup()
        {
            // Create Serial Port object
            _serialTransport = new SerialTransport
             {
                CurrentSerialSettings = { PortName = "COM6", BaudRate = 115200 } // object initializer
            };

            // Initialize the command messenger with the Serial Port transport layer
            // Set if it is communicating with a 16- or 32-bit Arduino board
            _cmdMessenger = new CmdMessenger( _serialTransport, BoardType.Bit32 );

            // Attach the callbacks to the Command Messenger
            AttachCommandCallBacks();

            // Start listening
            _cmdMessenger.Connect();

            _receivedItemsCount = 0;
            _receivedBytesCount = 0;

            // Clear queues 
            _cmdMessenger.ClearReceiveQueue();
            _cmdMessenger.ClearSendQueue();

            Thread.Sleep( 100 );

        }
        // This is the list of recognized commands. These can be commands that can either be sent or received. 

        
        // In order to receive, attach a callback function to these events
        enum StepperCommand
        {
            // Commands
            kAcknowledge, //0  Command to acknowledge that cmd was received
            kError, //1  Command to report errors
            kFloatAddition, //2  Command to request add two floats
            kFloatAdditionResult, //3  Command to report addition result
            kGetFwVerNum, //4  Command to get firmware version as a float
            kGetFwVerStr, //5  Command to get firmware version as a string
            kMoveToPosition, //6  Command to move current motor to absolute target position
            kGetCurrentMotorStatus, //7  Command to get current motor status
            kCurrentMotorStop, //8  Command to stop current motor move
            kAwaitCurrentMotorMoveDone, //9  Command to await motor move completion
            kInitializeAxis, //10 Command to init current axis using optical switch
            kSetMovePolarity, //11 Command to set move polarity; false is clockwise positive, reversed (true) is ccw positive
            kMoveRelative, //12 Command to move motor to relative target position
            kGetPosition, //13 Command to get current motor position
            kSetMaxTravel, //14 Command to set max travel distance
            kSetMaxSpeed, //15 Command to set max motor speed
            kSetCurrentScaling, //16 Command to set motor current scaling (% of max current) -- added in V1.0.1
            kSetCurrentAxis, //17 Command to set current axis (defaults to 1) -- added in V1.0.2
            kMultiMoveToPosition, //18 Command to move multiple motors to absolute target position -- added in V1.0.3
            kMultiMoveRelative, //19 Command to move multiple motors to relative target position -- added in V1.0.4
            kSetAcceleration, //20 Command to set acceleration value in steps/sec/sec (default is 50,000)
            kMoveContinuous, //21 Command to rotate until stopped
            kResetPosition, //22 Command to reset the current motor position to zero
            kSetMaxHomeSearchMove, //23 Command to set max search distance for home
            kSetHomingSpeed, //24 Command to set homing speed
            kGetHomeFlagState, //25 Command to get home flag state for current axis
            kGetLastInitPosition, //26 Command to get last axis init position
            kSetMotorEnabledState, //27 Command to set flag specifying whether motor is enabled
            kLEDsOff, //28 Command to set color of all NeoPixels to Off
            kLEDsIdle, //29 Command to set color of all NeoPixels to Blue
            kLEDsRun, //30 Command to send green Wipe pattern to NeoPixels
            kLEDsError, //31 Command to send red Flash pattern to NeoPixels
            kGetADCRawVoltage, //32 Command to get ADC value in unscaled units -- added in V1.0.18
            kSetOutHigh, //33 Command to set an output pin high
            kSetOutLow, //34 Command to set an output pin low
            kSetLimitSwitchPolarity, //35 Command to set polarity of switches used; T=default (false if blocked)
            kSetCurrentLimitSwitch, //36 Command to set currently active limit switch (it is auto-set on axis change)
            kHInitializeXY, //37 Command to init X axis of H-Bot using optical switch
            kHMoveRelative, //38 Command to move X,Y of H-Bot to relative target position
            kHMoveToPosition, //39 Command to move X,Y of H-Bot to absolute target position; 3 params (1st param is 1, 2, 3 for X, Y, Both)
            kHGetXY, //40 Command to get X,Y coordinate of H-Bot
            kGetDebugStr, //41 Command to get Debug string
            kHMoveDoneMssg, //42 Message to communicate that an H-move is complete: HInitializeXY, HMoveRelative, HMoveToPosition
            kSetHoldCurrentScaling, //43 for compatibility with L6470 firmware
            kSetMicroStepModeL6470, //44 for compatibility with L6470 firmware
            kLEDsSetColor, //45 Command to set color of each NeoPixel (3)
            kEnableEOT, //46 Command to enable all EOT sensors to stop motion on change
            kDisableEOT, //47 Command to disable all EOT sensors to stop motion on change
            kSet2209StallThresh, //48 Command to set TMC2209 stall threshold value
            kSet2209MotorCurrent, //49 Command to set TMC2209 motor current in mA
            kSet2209MicroStepMode, //50 Command to set TMC2209 microstep mode -- param is 2,4,8,16,...,256
            kStopOn2209Stall, //51 Command to set TMC2209 to stop when threshold exceeded -- param T|F
            kInit2209, //52 Command to initialize the TMC2209
            kSensorOverride, //53 Command to stop on any of the 8 sensors
            kSensorPolarity, //54 Command to set polarity of any of the 8 sensors
            kGetSensorState, //55 Command to get state of any of the 8 sensors
            kSetFlexiForceStallThresh, //56 Command to set FlexiForce stall threshold value for current motor
            kStopOnFlexiForceStall, //57 Command to set current motor to stop when FlexiForce threshold exceeded -- param T|F
            kGetFlexiForce, //58 Command to get last value of FlexiForce Sensor specified in the param (1 or 2)
            kGetSwitchSet, //59 Command to get last value of Switch Block specified in the param (1 or 2 -- 1=microswitches, 2=optoswitches)
            kStreamFlexiForceDebug, //60 Command to stream FlexiForce debug data -- param T|F
            kStreamSwitchSetDebug, //61 Command to stream Switch settings (debug); Switch Block specified in the param (1 or 2 -- 1=microswitches, 2=optoswitches)
            kCallbacksForSwitchChanges, //62 Command to enable or disable callbacks on Switch Changes
            kLEDsSetPattern, //63 Command to set color and pattern of each NeoPixel (3) -- added V1.26
            kLEDsSetBrightness, //64 Command to set brightness of NeoPixels (3) -- added V1.26
            kGetReflectiveSensorValue, //65 Command to get last value of Reflective Sensor specified in the param (1 or 2)
            kStreamReflectiveValues, //66 Command to stream Reflective debug data -- param T|F
            kGetLightSensorValue, //67 Command to get last value of Light Sensor specified in the param (1 or 2)
            kStreamLightValues, //68 Command to stream Light debug data -- param T|F
        //------------------------------------------------------------------------------------------------------------------------------------------
        };

        

        /// Attach command call backs. 
        private void AttachCommandCallBacks()
        {
            _cmdMessenger.Attach( OnUnknownCommand );
            _cmdMessenger.Attach( (int)StepperCommand.kGetCurrentMotorStatus, OnGetCurrentMotorStatus );
            _cmdMessenger.Attach( (int)StepperCommand.kAcknowledge, OnAcknowledge );
            _cmdMessenger.Attach( (int)StepperCommand.kError, OnError );
            _cmdMessenger.Attach( (int)StepperCommand.kGetDebugStr, OnGetDebugStr );
            _cmdMessenger.Attach( (int)StepperCommand.kHMoveDoneMssg, OnHMoveDoneMssg );
        }

        // Called when a received command has no attached function.
        void OnUnknownCommand( ReceivedCommand arguments )
        {
            Console.WriteLine( "Command without attached callback received" );
        }

        void OnGetCurrentMotorStatus( ReceivedCommand arguments )
        {
            Console.WriteLine( "Command without attached callback received" );
        }

        void OnAcknowledge( ReceivedCommand arguments )
        {
            Console.WriteLine( "Command without attached callback received" );
        }

        void OnError( ReceivedCommand arguments )
        {
            Console.WriteLine( "Command without attached callback received" );
        }
        void OnGetDebugStr( ReceivedCommand arguments )
        {
            Console.WriteLine( "Command without attached callback received" );
        }
        void OnHMoveDoneMssg( ReceivedCommand arguments )
        {
            Console.WriteLine( "Command without attached callback received" );
        }

        public void Exit()
        {
            // Stop listening
            _cmdMessenger.Disconnect();

            // Dispose Command Messenger
            _cmdMessenger.Dispose();

            // Dispose Serial Port object
            _serialTransport.Dispose();

            // Pause before stop
            //Console.WriteLine( "Press any key to stop..." );
            //Console.ReadKey();
        }
        /*
                public class StepperControllerOld
                {
                    public bool RunLoop { get; set; }
                    private SerialTransport _serialTransport;
                    private CmdMessenger _cmdMessenger;
                    private int _receivedItemsCount;                        // Counter of number of plain text items received
                    private int _receivedBytesCount;                        // Counter of number of plain text bytes received
                    long _beginTime;                                        // Start time, 1st item of sequence received 
                    long _endTime;                                          // End time, last item of sequence received 
                    private bool _receivePlainTextFloatSeriesFinished;      // Indicates if plain text float series has been fully received
                    private bool _receiveBinaryFloatSeriesFinished;         // Indicates if binary float series has been fully received
                    const int SeriesLength = 2000;                          // Number of items we like to receive from the Arduino
                    private const float SeriesBase = 1111111.111111F;       // Base of values to return: SeriesBase * (0..SeriesLength-1)



                    // Setup function
                    public void Setup()
                    {
                        // Create Serial Port object
                        _serialTransport = new SerialTransport
                        {
                            CurrentSerialSettings = { PortName = "COM6", BaudRate = 115200 } // object initializer
                        };

                        // Initialize the command messenger with the Serial Port transport layer
                        // Set if it is communicating with a 16- or 32-bit Arduino board
                        _cmdMessenger = new CmdMessenger( _serialTransport, BoardType.Bit32 );

                        // Attach the callbacks to the Command Messenger
                        //AttachCommandCallBacks();

                        // Start listening
                        _cmdMessenger.Connect();

                        _receivedItemsCount = 0;
                        _receivedBytesCount = 0;

                        // Clear queues 
                        _cmdMessenger.ClearReceiveQueue();
                        _cmdMessenger.ClearSendQueue();

                        Thread.Sleep( 100 );

                        // Send command requesting a series of 100 float values send in plain text form
                        //var commandPlainText = new SendCommand( (int)Command.RequestPlainTextFloatSeries );
                        //commandPlainText.AddArgument( (UInt16)SeriesLength );
                       // commandPlainText.AddArgument( (float)SeriesBase );
                        // Send command 
                        //_cmdMessenger.SendCommand( commandPlainText );

                        // Now wait until all values have arrived
                        //while (!_receivePlainTextFloatSeriesFinished)
                        //{
                            Thread.Sleep( 100 );
                        }


                        // Clear queues 
                        _cmdMessenger.ClearReceiveQueue();
                        _cmdMessenger.ClearSendQueue();

                        _receivedItemsCount = 0;
                        _receivedBytesCount = 0;
                        // Send command requesting a series of 100 float values send in binary form
                        //var commandBinary = new SendCommand( (int)Command.RequestBinaryFloatSeries );
                        //commandBinary.AddBinArgument( (UInt16)SeriesLength );
                       // commandBinary.AddBinArgument( (float)SeriesBase );

                        // Send command 
                        //_cmdMessenger.SendCommand( commandBinary );

                        // Now wait until all values have arrived
                        //while (!_receiveBinaryFloatSeriesFinished)
                        //{
                        //    Thread.Sleep( 100 );
                        //}
                    }

                    // Loop function
                    public void Loop()
                    {
                        RunLoop = false;
                    }

                    // Exit function
                    public void Exit()
                    {
                        // Stop listening
                        _cmdMessenger.Disconnect();

                        // Dispose Command Messenger
                        _cmdMessenger.Dispose();

                        // Dispose Serial Port object
                        _serialTransport.Dispose();

                        // Pause before stop
                        Console.WriteLine( "Press any key to stop..." );
                        Console.ReadKey();
                    }



                    // ------------------  C A L L B A C K S ---------------------

                    // Called when a received command has no attached function.
                    void OnUnknownCommand( ReceivedCommand arguments )
                    {
                        Console.WriteLine( "Command without attached callback received" );
                    }


                    // Callback function To receive the plain text float series from the Arduino
                    void OnReceivePlainTextFloatSeries( ReceivedCommand arguments )
                    {
                        _receivedBytesCount += CountBytesInCommand( arguments, true );

                        var count = arguments.ReadInt16Arg();
                        var receivedValue = arguments.ReadFloatArg();


                        if (count != _receivedItemsCount)
                        {
                            Console.WriteLine( "Values not matching: received {0} expected {1}", count, _receivedItemsCount );
                        }
                        if (_receivedItemsCount % (SeriesLength / 10) == 0)
                            Console.WriteLine( "Received value: {0}", receivedValue );
                        if (_receivedItemsCount == 0)
                        {
                            // Received first value, start stopwatch
                            _beginTime = Millis;
                        }
                        else if (count == SeriesLength - 1)
                        {
                            // Received all values, stop stopwatch
                            _endTime = Millis;
                            var deltaTime = (_endTime - _beginTime);
                            Console.WriteLine( "{0} milliseconds per {1} items = is {2} ms/item, {3} Hz",
                                deltaTime,
                                SeriesLength,
                                (float)deltaTime / (float)SeriesLength,
                                (float)1000 * SeriesLength / (float)deltaTime
                                );
                            Console.WriteLine( "{0} milliseconds per {1} bytes = is {2} ms/byte,  {3} bytes/sec, {4} bps",
                                deltaTime,
                                _receivedBytesCount,
                                (float)deltaTime / (float)_receivedBytesCount,
                                (float)1000 * _receivedBytesCount / (float)deltaTime,
                                (float)8 * 1000 * _receivedBytesCount / (float)deltaTime
                                );
                            _receivePlainTextFloatSeriesFinished = true;
                        }
                        _receivedItemsCount++;
                    }

                    private int CountBytesInCommand( CommandMessenger.Command command, bool printLfCr )
                    {
                        var bytes = command.CommandString().Length; // Command + command separator
                                                                    //var bytes = _cmdMessenger.CommandToString(command).Length + 1; // Command + command separator
                        if (printLfCr) bytes += 2; // Add  bytes for carriage return ('\r') and /or a newline  ('\n')
                        return bytes;
                    }

                    // Callback function To receive the binary float series from the Arduino
                    void OnReceiveBinaryFloatSeries( ReceivedCommand arguments )
                    {
                        var count = arguments.ReadBinUInt16Arg();
                        var receivedValue = arguments.ReadBinFloatArg();

                        _receivedBytesCount += CountBytesInCommand( arguments, false );

                        if (count != _receivedItemsCount)
                        {
                            Console.WriteLine( "Values not matching: received {0} expected {1}", count, _receivedItemsCount );
                        }

                        if (_receivedItemsCount % (SeriesLength / 10) == 0)
                            Console.WriteLine( "Received value: {0}", receivedValue );
                        if (_receivedItemsCount == 0)
                        {
                            // Received first value, start stopwatch
                            _beginTime = Millis;
                        }
                        else if (count == SeriesLength - 1)
                        {
                            // Received all values, stop stopwatch
                            _endTime = Millis;
                            var deltaTime = (_endTime - _beginTime);
                            Console.WriteLine( "{0} milliseconds per {1} items = is {2} ms/item, {3} Hz",
                                deltaTime,
                                SeriesLength,
                                (float)deltaTime / (float)SeriesLength,
                                (float)1000 * SeriesLength / (float)deltaTime
                                );
                            Console.WriteLine( "{0} milliseconds per {1} bytes = is {2} ms/byte,  {3} bytes/sec, {4} bps",
                                deltaTime,
                                _receivedBytesCount,
                                (float)deltaTime / (float)_receivedBytesCount,
                                (float)1000 * _receivedBytesCount / (float)deltaTime,
                                (float)8 * 1000 * _receivedBytesCount / (float)deltaTime
                                );
                            _receiveBinaryFloatSeriesFinished = true;
                        }
                        _receivedItemsCount++;
                    }

                    // Return Milliseconds since 1970
                    public static long Millis { get { return (long)((DateTime.Now.ToUniversalTime() - new DateTime( 1970, 1, 1, 0, 0, 0, DateTimeKind.Utc )).TotalMilliseconds); } }
                }*/
    }
}