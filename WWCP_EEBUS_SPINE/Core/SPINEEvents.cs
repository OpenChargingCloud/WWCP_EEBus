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
    /// Who gets to see an event first.
    /// </summary>
    public enum SPINEEventLevel
    {

        /// <summary>
        /// The stack itself - the heartbeat monitor, the managers. These run
        /// first, because what they do changes what the application then sees.
        /// </summary>
        Core,

        /// <summary>
        /// Everything built on top: the use cases, the test bench, the user.
        /// </summary>
        Application

    }


    /// <summary>
    /// Something happened which somebody may want to know about.
    /// </summary>
    /// <param name="Timestamp">When it happened, by the time provider of the device.</param>
    public abstract record SPINEEvent(DateTimeOffset Timestamp);


    /// <summary>
    /// The data of a function changed, because another device said so or asked
    /// for it.
    /// </summary>
    /// <param name="Timestamp">When it happened.</param>
    /// <param name="Change">What changed.</param>
    public sealed record SPINEDataChanged(DateTimeOffset   Timestamp,
                                          SPINEDataChange  Change)

        : SPINEEvent(Timestamp)

    {

        /// <summary>Return a text representation of this event.</summary>
        public override String ToString()
            => Change.ToString();

    }


    /// <summary>
    /// The detailed discovery of another device arrived: its entities and
    /// features are now known.
    /// </summary>
    /// <param name="Timestamp">When it happened.</param>
    /// <param name="RemoteDevice">The device.</param>
    public sealed record SPINEDeviceDiscovered(DateTimeOffset     Timestamp,
                                               SPINERemoteDevice  RemoteDevice)

        : SPINEEvent(Timestamp)

    {

        /// <summary>Return a text representation of this event.</summary>
        public override String ToString()
            => $"discovered {RemoteDevice}";

    }


    /// <summary>
    /// Another device announced an entity, or announced that one is gone.
    /// </summary>
    /// <param name="Timestamp">When it happened.</param>
    /// <param name="RemoteEntity">The entity.</param>
    /// <param name="Added">Whether it appeared or disappeared.</param>
    public sealed record SPINEEntityChanged(DateTimeOffset     Timestamp,
                                            SPINERemoteEntity  RemoteEntity,
                                            Boolean            Added)

        : SPINEEvent(Timestamp)

    {

        /// <summary>Return a text representation of this event.</summary>
        public override String ToString()
            => $"{(Added ? "added" : "removed")} {RemoteEntity}";

    }


    /// <summary>
    /// A subscription or a binding was agreed to, or given up.
    /// </summary>
    /// <param name="Timestamp">When it happened.</param>
    /// <param name="Relation">The relation.</param>
    /// <param name="Kind">Whether it is a subscription or a binding.</param>
    /// <param name="Added">Whether it was agreed to or given up.</param>
    public sealed record SPINERelationChanged(DateTimeOffset        Timestamp,
                                              SPINEFeatureRelation  Relation,
                                              String                Kind,
                                              Boolean               Added)

        : SPINEEvent(Timestamp)

    {

        /// <summary>Return a text representation of this event.</summary>
        public override String ToString()
            => $"{(Added ? "added" : "removed")} {Kind} {Relation}";

    }


    /// <summary>
    /// A datagram was refused, with the result which was sent back.
    /// </summary>
    /// <param name="Timestamp">When it happened.</param>
    /// <param name="Datagram">The datagram.</param>
    /// <param name="Result">The result which was sent back.</param>
    public sealed record SPINEDatagramRefused(DateTimeOffset  Timestamp,
                                              DatagramType    Datagram,
                                              ResultDataType  Result)

        : SPINEEvent(Timestamp)

    {

        /// <summary>Return a text representation of this event.</summary>
        public override String ToString()
            => $"refused: {Result}";

    }


    /// <summary>
    /// A device which was sending heartbeats has stopped.
    /// </summary>
    /// <param name="Timestamp">When this was noticed.</param>
    /// <param name="RemoteFeature">The feature which was sending them.</param>
    /// <param name="LastSeen">When the last one arrived.</param>
    /// <param name="Timeout">Within which time one was expected.</param>
    public sealed record SPINEHeartbeatMissing(DateTimeOffset      Timestamp,
                                               SPINERemoteFeature  RemoteFeature,
                                               DateTimeOffset      LastSeen,
                                               TimeSpan            Timeout)

        : SPINEEvent(Timestamp)

    {

        /// <summary>Return a text representation of this event.</summary>
        public override String ToString()
            => $"no heartbeat from {RemoteFeature.Address} for {Timestamp - LastSeen} (expected every {Timeout})";

    }


    /// <summary>
    /// What a device tells the world about itself.
    ///
    /// Two levels, and the order between them is the point: the stack itself
    /// reacts first, and only then does anything built on top get to see the
    /// event - because a use case which is told that an entity disappeared
    /// should not still find its subscriptions in place.
    ///
    /// Both levels run one after the other on the thread which published, unlike
    /// the Go reference implementation, which runs the application handlers on a
    /// goroutine each. A test bench which cannot say whether an event has been
    /// handled yet cannot assert anything about it, and an event handler which
    /// takes long enough to matter is a problem to see rather than to hide. A
    /// handler which throws is caught and reported through
    /// <see cref="OnHandlerFailed"/>: it must not be able to stop a datagram
    /// from being processed.
    /// </summary>
    public class SPINEEvents
    {

        #region Data

        private readonly Lock                                             handlersLock  = new ();

        private readonly List<(SPINEEventLevel Level, Action<SPINEEvent> Handler)>  handlers = [];

        private readonly TimeProvider                                     timeProvider;

        #endregion

        #region Events

        /// <summary>
        /// An event handler threw. The event was delivered to every other
        /// handler anyway.
        /// </summary>
        public event Action<SPINEEvent, Exception>? OnHandlerFailed;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Create the events of a device.
        /// </summary>
        /// <param name="TimeProvider">Where the timestamps of the events come from.</param>
        public SPINEEvents(TimeProvider? TimeProvider = null)
        {
            this.timeProvider = TimeProvider ?? System.TimeProvider.System;
        }

        #endregion


        #region Subscribe(Handler, Level = Application) / Unsubscribe(Handler)

        /// <summary>
        /// Be told about everything which happens.
        /// </summary>
        /// <param name="Handler">What to do with an event.</param>
        /// <param name="Level">Whether this is a part of the stack or something built on it.</param>
        public void Subscribe(Action<SPINEEvent>  Handler,
                              SPINEEventLevel     Level   = SPINEEventLevel.Application)
        {
            lock (handlersLock)
            {

                if (!handlers.Any(entry => entry.Level == Level && entry.Handler == Handler))
                    handlers.Add((Level, Handler));

            }
        }


        /// <summary>
        /// Stop being told.
        /// </summary>
        /// <param name="Handler">The handler which was subscribed.</param>
        public Boolean Unsubscribe(Action<SPINEEvent> Handler)
        {
            lock (handlersLock)
            {
                return handlers.RemoveAll(entry => entry.Handler == Handler) > 0;
            }
        }


        /// <summary>
        /// Be told about events of one kind only.
        /// </summary>
        /// <typeparam name="T">A kind of event.</typeparam>
        /// <param name="Handler">What to do with it.</param>
        /// <param name="Level">Whether this is a part of the stack or something built on it.</param>
        /// <returns>The handler which was subscribed, so that it can be unsubscribed again.</returns>
        public Action<SPINEEvent> Subscribe<T>(Action<T>        Handler,
                                               SPINEEventLevel  Level   = SPINEEventLevel.Application)

            where T : SPINEEvent

        {

            void handler(SPINEEvent @event)
            {
                if (@event is T typed)
                    Handler(typed);
            }

            Subscribe(handler, Level);

            return handler;

        }

        #endregion

        #region Publish(Event) / Publish(Factory)

        /// <summary>
        /// Tell everybody about an event: the core first, then the application.
        /// </summary>
        /// <param name="Event">What happened.</param>
        public void Publish(SPINEEvent Event)
        {

            (SPINEEventLevel Level, Action<SPINEEvent> Handler)[] current;

            lock (handlersLock)
            {
                current = [.. handlers];
            }

            foreach (var level in new[] { SPINEEventLevel.Core, SPINEEventLevel.Application })
                foreach (var entry in current)
                {

                    if (entry.Level != level)
                        continue;

                    try
                    {
                        entry.Handler(Event);
                    }
                    catch (Exception e)
                    {
                        OnHandlerFailed?.Invoke(Event, e);
                    }

                }

        }


        /// <summary>
        /// Tell everybody about an event, whose timestamp comes from the time
        /// provider of the device.
        /// </summary>
        /// <param name="Factory">How to build the event, given the current time.</param>
        public void Publish(Func<DateTimeOffset, SPINEEvent> Factory)
        {
            Publish(Factory(timeProvider.GetUtcNow()));
        }

        #endregion

    }

}
