using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrderFlow.Application.Features.Orders.GetOrderById
{
    public sealed class GetOrderByIdCommandHandler
        : IQueryHandler<GetOrderByIdCommand, GetOrderByIdResult>
    {
        private readonly IOrderRepository _orderRepository;

        public GetOrderByIdCommandHandler(IOrderRepository orderRepository)
        {
            _orderRepository = orderRepository;
        }

        public async Task<GetOrderByIdResult> Handle(
            GetOrderByIdCommand query,
            CancellationToken cancellationToken)
        {
            var order = await _orderRepository.GetByIdAsync(
                query.OrderId,
                cancellationToken);

            if (order is null)
            {

                throw new NotFoundException(
                    "Order",
                    query.OrderId);
            }

            return new GetOrderByIdResult(
                order.Id,
                order.CustomerId,
                order.Status.ToString(),
                order.TotalAmount,
                order.Items
                    .Select(item => new GetOrderByIdItemResult(
                        item.ProductId,
                        item.Quantity,
                        item.UnitPrice,
                        item.Subtotal))
                    .ToList());
        }

    }
}