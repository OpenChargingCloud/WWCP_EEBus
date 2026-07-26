/*
 * Copyright (c) 2014-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of WWCP EEBUS <https://github.com/OpenChargingCloud/WWCP_EEBUS>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using cloud.charging.open.protocols.EEBUS.SPINE.Model;

#endregion

namespace cloud.charging.open.protocols.EEBUS.SPINE
{

    /// <summary>
    /// The data of one function of one feature, and what may be done with it.
    ///
    /// A feature is a set of functions, and a function is a piece of data plus
    /// the operations the feature offers on it. Both halves live here: the data
    /// itself, which only ever leaves this class as a copy, and the
    /// "possibleOperations" the feature announces for it in the detailed
    /// discovery.
    ///
    /// There is no class per function and no generic parameter. Which type
    /// belongs to "loadControlLimitListData" is in the generated function
    /// registry, and what an update of it means is in the generic update engine
    /// of WP06c - so this class is the same code for all 142 of them.
    /// </summary>
    public class SPINEFunctionData
    {

        #region Data

        private readonly Lock     dataLock  = new ();

        private          Object?  data;

        #endregion

        #region Properties

        /// <summary>
        /// The name of the function.
        /// </summary>
        public String                  Function      { get; }

        /// <summary>
        /// What the function registry says its data type is.
        /// </summary>
        public Type                    DataType      { get; }

        /// <summary>
        /// Which operations this feature offers on this function.
        /// </summary>
        public PossibleOperationsType  Operations    { get; set; }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create the data of one function of one feature.
        /// </summary>
        /// <param name="Function">The name of a SPINE function.</param>
        /// <param name="Operations">Which operations the feature offers on it.</param>
        public SPINEFunctionData(String                   Function,
                                 PossibleOperationsType?  Operations   = null)
        {

            var info = SPINEFunctions.Get(Function)
                           ?? throw new ArgumentException($"'{Function}' is not a function of SPINE {Version.String}.",
                                                          nameof(Function));

            this.Function    = Function;
            this.DataType    = info.DataType;
            this.Operations  = Operations ?? PossibleOperationsType.ReadAndMaybeWrite();

        }

        #endregion


        #region DataCopy()

        /// <summary>
        /// A copy of the data, or null when the function holds none.
        ///
        /// A copy, because the caller may keep it for as long as it likes while
        /// the next datagram changes what the device holds.
        /// </summary>
        public Object? DataCopy()
        {
            lock (dataLock)
            {
                return SPINETypeInfo.Clone(data);
            }
        }


        /// <summary>
        /// A copy of the data as the given type, or null when the function holds
        /// none or holds something else.
        /// </summary>
        /// <typeparam name="T">The data type of the function.</typeparam>
        public T? DataCopy<T>() where T : class

            => DataCopy() as T;

        #endregion

        #region SetData(Data)

        /// <summary>
        /// Replace the data of this function, without asking anybody: this is
        /// the device changing its own data.
        /// </summary>
        /// <param name="Data">The new data, or null to forget it.</param>
        public void SetData(Object? Data)
        {

            if (Data is not null && !DataType.IsInstanceOfType(Data))
                throw new ArgumentException($"'{Function}' holds a '{DataType.Name}', not a '{Data.GetType().Name}'.",
                                            nameof(Data));

            lock (dataLock)
            {
                data = SPINETypeInfo.Clone(Data);
            }

        }

        #endregion

        #region UpdateData(Data, Cmd, Options)

        /// <summary>
        /// Apply a command to the data of this function.
        /// </summary>
        /// <param name="Data">The data the command carries, or null when it carries none.</param>
        /// <param name="Cmd">The command, whose filters say which part of the function is meant.</param>
        /// <param name="Options">How the update is to be applied.</param>
        public SPINEUpdateResult<Object> UpdateData(Object?             Data,
                                                    CmdType             Cmd,
                                                    SPINEUpdateOptions  Options)
        {

            if (Data is not null && !DataType.IsInstanceOfType(Data))
                return new (null,
                            false,
                            $"'{Function}' holds a '{DataType.Name}', but the command carries a '{Data.GetType().Name}'.");

            lock (dataLock)
            {

                var result = SPINEUpdate.Apply(data, Data, Cmd, Options);

                // A refused write answers with the data as it was, so assigning
                // it back is right in either case.
                data = result.Data;

                return result;

            }

        }

        #endregion


        #region ToCmd     (Partial = false)

        /// <summary>
        /// This function as a command carrying its data.
        /// </summary>
        /// <param name="Partial">Whether to mark the command as a partial one.</param>
        public CmdType ToCmd(Boolean Partial = false)
        {

            var cmd = new CmdType();

            cmd.SetData(Function, DataCopy());

            if (Partial)
            {
                cmd.Function  = FunctionType.Parse(Function);
                cmd.Filter    = [ new FilterType { CmdControl = CmdControlType.ForPartial } ];
            }

            return cmd;

        }

        #endregion

        #region ToProperty()

        /// <summary>
        /// This function as the detailed discovery states it: its name together
        /// with the operations the feature offers on it.
        /// </summary>
        public FunctionPropertyType ToProperty()

            => new () {
                   Function            = FunctionType.Parse(Function),
                   PossibleOperations  = Operations
               };

        #endregion

        #region (override) ToString()

        /// <summary>
        /// Return a text representation of this function.
        /// </summary>
        public override String ToString()

            => $"{Function} ({(Operations.CanWrite ? "read/write" : "read only")}" +
               $"{(data is null ? ", empty" : "")})";

        #endregion

    }

}
