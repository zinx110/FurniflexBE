﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using FurniflexBE.Context;
using FurniflexBE.DTOModels;
using FurniflexBE.Helpers;
using FurniflexBE.Models;

namespace FurniflexBE.Controllers
{
    [Authorize]
    public class OrdersController : ApiController
    {
        private AppDbContext db = new AppDbContext();

        // GET: api/Orders
        public IHttpActionResult GetOrders()
        {
            var userRole = IdentityHelper.GetRoleName(User.Identity as ClaimsIdentity);

            if (userRole != "admin")
            {
                return Unauthorized(); // Returns 401 Unauthorized if the user is not an admin
            }

            var orders = db.orders.ToList(); // Get all orders as a list

            return Ok(orders); // Return the orders wrapped in an Ok response
        }

        // GET: api/Orders/5
        [ResponseType(typeof(Order))]
        public async Task<IHttpActionResult> GetOrder(int id)
        {
            Order order = await db.orders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            return Ok(order);
        }

        [HttpGet, Route("api/Orders/MyOrders")]
        [ResponseType(typeof(Order))]
        public async Task<IHttpActionResult> MyOrders()
        {
            var userId = IdentityHelper.GetUserId(User.Identity as ClaimsIdentity);
            if (userId == null)
            {
                return Unauthorized();
            }
            var orders = await db.orders.Where(o => o.UserId == userId).Include(o=>o.OrderItems.Select(oi=>oi.Product)).ToListAsync();
            return Ok(orders);


        }

        // PUT: api/Orders/5
        [ResponseType(typeof(void))]
        public async Task<IHttpActionResult> PutOrder(int id,[FromBody] OrderUpdateDTO orderUpdateDTO)
        {
            var userRole = IdentityHelper.GetRoleName(User.Identity as ClaimsIdentity);
            if (userRole != "admin")
            {
                return BadRequest($"User Role is {userRole}. and not 'admin'. User cannot do the action");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var order = await db.orders.FindAsync(id);
            if (order ==null)
            {
                return BadRequest();
            }

            order.OrderStatus = orderUpdateDTO.OrderStatus;

            db.Entry(order).State = EntityState.Modified;

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!OrderExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return StatusCode(HttpStatusCode.NoContent);
        }

        // POST: api/Orders

        [ResponseType(typeof(Order))]
        public async Task<IHttpActionResult> PostOrder([FromBody] OrderDTO orderDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = IdentityHelper.GetUserId(User.Identity as ClaimsIdentity);
            if(userId == null)
            {
                return Unauthorized();
            }

            // Create the order
            var order = new Order
            {
                UserId = (int)userId,
                TotalPrice = orderDto.SubTotal, // Subtotal for the order
                OrderStatus = "Processing",        // Initial status
                PaymentStatus = "Paid",       // Initial payment status
                CreatedAt = DateTime.Now,
                OrderItems = new List<OrderItem>()
            };

            // Map the OrderItems from DTO to the OrderItems in the Order
            foreach (var cart in orderDto.CartItems)
            {
                var prod = await db.products.FindAsync(cart.ProductId);

                if (prod == null)
                {
                    return NotFound();
                }

                // Check if there is enough stock
                if (prod.Quantity < cart.Quantity)
                {
                    return BadRequest($"Not enough stock for product: {prod.Name}. Available quantity: {prod.Quantity}");
                }

                // Decrease the product quantity in the database
                prod.Quantity -= cart.Quantity;

                var orderItem = new OrderItem
                {
                    ProductId = cart.ProductId,
                    Quantity = cart.Quantity,
                    Price = prod.DiscountedPrice,
                    Order = order
                };

                order.OrderItems.Add(orderItem);
            }

            // Add the order to the database
            db.orders.Add(order);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return InternalServerError(ex); // Handle any errors during save
            }

            // Return the newly created order
            return CreatedAtRoute("DefaultApi", new { id = order.OrderId }, order);
        }





        

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private bool OrderExists(int id)
        {
            return db.orders.Count(e => e.OrderId == id) > 0;
        }
    }
}