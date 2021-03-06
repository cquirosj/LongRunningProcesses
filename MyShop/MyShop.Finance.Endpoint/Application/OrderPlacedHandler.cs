﻿namespace MyShop.Finance.Endpoint.Application
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using MyShop.Finance.Endpoint.Domain;
    using MyShop.ItOps.Messages;
    using MyShop.Sales.Messages;

    using NServiceBus;
    using NServiceBus.Logging;
    using NServiceBus.MessageRouting.RoutingSlips;

    public class OrderPlacedHandler : IHandleMessages<IOrderPlaced>
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(OrderPlacedHandler));

        private readonly IOrderRepository orders;

        public OrderPlacedHandler(IOrderRepository orders)
        {
            this.orders = orders;
        }

        public Task Handle(IOrderPlaced message, IMessageHandlerContext context)
        {
            Log.Info($"Order with Id {message.OrderId} placed. Validating...");

            var order = new Order(message.OrderId, message.TotalValue);
            order.Status = OrderStatus.Validating;

            orders.Save(order);

            var validateCreditCardCharge =
                new ValidateCreditCardCharge { Amount = message.TotalValue, CorrelationId = order.OrderId };

            List<string> destinations = GetDestinationsFor(validateCreditCardCharge);
            context.Route(validateCreditCardCharge, order.OrderId, destinations.ToArray());

            return Task.CompletedTask;
        }

        private static List<string> GetDestinationsFor(ValidateCreditCardCharge validateCreditCardCharge)
        {
            var destinations = new List<string> { "ItOps.FreeCreditCardValidator.Endpoint" };
            if (validateCreditCardCharge.Amount > 500)
            {
                destinations.Add("ItOps.ExpensiveCreditCardValidator.Endpoint");
            }

            if (validateCreditCardCharge.Amount > 2000)
            {
                destinations.Add("ItOps.VeryExpensiveFraudDetection.Endpoint");
            }

            destinations.Add("Finance.Endpoint");
            return destinations;
        }
    }
}