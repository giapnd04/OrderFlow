# OrderFlow — Software Requirements Specification (SRS)

**Version:** 1.0
**Status:** Draft / Baseline
**Project:** OrderFlow — Enterprise Order Management System
**Target Role:** Backend .NET Developer Portfolio Project

---

# 1. Introduction

## 1.1 Purpose

OrderFlow is an Enterprise Order Management System designed to manage the lifecycle of customer orders from the point at which an order request is recorded in the system through fulfillment, delivery, completion, cancellation, and post-order adjustments where applicable.

The project is intended to demonstrate practical backend engineering skills together with an understanding of real-world software development processes, business rules, data integrity, authorization, auditability, and system design.

---

## 1.2 Business Problem

In a manual order-management environment using spreadsheets, email, or disconnected processes, the business may encounter:

* Incorrect order information such as price, quantity, or delivery address.
* Duplicate orders created by different employees.
* Conflicting or overwritten data when multiple employees modify the same information.
* Missing or incorrect customer information.
* Delayed communication between departments.
* Difficulty tracking the current state of an order.
* Inconsistent inventory, price, and order information.
* Time-consuming reporting and investigation.
* Difficulty determining who changed what and when.
* Risk of customer-data leakage through copied spreadsheets or uncontrolled files.

Among these problems, incorrect order data is considered the most serious because an incorrect order can lead to:

```text
Incorrect order data
        ↓
Incorrect price / quantity / product information
        ↓
Incorrect inventory handling
        ↓
Incorrect fulfillment / delivery
        ↓
Customer complaint
        ↓
Financial loss + operational cost + reputational impact
```

---

# 2. Business Goals

OrderFlow aims to provide the following business outcomes:

## 2.1 Reduce Order Processing Errors

The system should reduce errors involving:

* Product
* Quantity
* Price
* Customer information
* Delivery information
* Order status

---

## 2.2 Improve Order Processing Efficiency

The business should be able to compare the average time required to perform equivalent order-processing tasks before and after using the system.

Example metric:

```text
Average Order Processing Time
Before OrderFlow
vs.
After OrderFlow
```

---

## 2.3 Improve Data Integrity

The system should prevent uncontrolled overwriting and conflicting modifications of order information.

Changes to important transactional information should be controlled according to the order lifecycle.

---

## 2.4 Improve Traceability

The system should make it possible to determine:

* Who performed a change.
* What was changed.
* When the change occurred.
* Where applicable, the previous and new values.

This should reduce the time required to investigate order-related problems.

---

## 2.5 Improve Operational Visibility

Authorized users should be able to understand:

* Current order state.
* Order progress.
* Fulfillment status.
* Delivery status.
* Payment status.
* Relevant exceptions.

---

# 3. Target Users

## 3.1 Sales

Responsibilities:

* Receive or record order requests.
* Enter customer and order information.
* Validate order information.
* Confirm orders.
* Handle customer-related order changes.

---

## 3.2 Warehouse Staff

Responsibilities:

* View confirmed orders requiring fulfillment.
* Check required products and quantities.
* Prepare orders.
* Update fulfillment-related status.
* Report fulfillment problems.

Warehouse staff should not directly modify important order information once fulfillment has started.

---

## 3.3 Delivery Staff

Responsibilities:

* View delivery information.
* Process delivery.
* Update delivery-related status.
* Report delivery failures.

---

## 3.4 Customer Service

Responsibilities:

* View order information.
* View order status.
* Assist customers with order-related issues.
* Handle customer requests and problems.

---

## 3.5 Operator / Manager

Responsibilities:

* Monitor orders.
* Detect errors and delays.
* Monitor operational activities.
* Review reports.
* Investigate operational issues.

---

# 4. Other Stakeholders

## 4.1 Customer

The customer is affected by:

* Incorrect order information.
* Incorrect price or quantity.
* Delivery problems.
* Cancellation.
* Payment problems.
* Returns and refunds.

---

## 4.2 Business Owner

The business owner is affected by:

* Revenue impact.
* Incorrect orders.
* Operational efficiency.
* Business decisions based on order data.

---

## 4.3 Accounting

Accounting may depend on accurate:

* Prices.
* Quantities.
* Order totals.
* Payment information.
* Refunds and adjustments.

OrderFlow does not own the General Ledger / accounting system.

---

# 5. System Scope

## 5.1 In Scope

The core OrderFlow system includes:

* Customer management
* Product management
* Inventory management
* Order management
* Payment status management
* Delivery management

Supporting capabilities include:

* Order Change / Adjustment
* Return / Refund
* User / Employee management
* Authorization / Business Permission
* Audit / History
* Notification
* Reporting / Operations

---

## 5.2 Out of Scope

The following are explicitly outside the current project scope:

* Procurement / Purchasing
* Supplier Management
* Manufacturing / Production
* Accounting / General Ledger
* HR / Payroll
* Marketing / CRM Campaign Management
* Advanced Warehouse Management
* Implementation of external Payment Gateway internals
* Implementation of external Delivery Provider internals

OrderFlow may integrate with external systems in the future, but does not own their internal business processes.

---

# 6. System Boundary

OrderFlow begins when an **Order Request is recorded in the system**.

The system does not attempt to model the customer's decision to purchase.

### Inside the boundary

OrderFlow is responsible for:

```text
Order Request
     ↓
Order Creation
     ↓
Validation
     ↓
Confirmation
     ↓
Inventory Handling
     ↓
Payment Status Management
     ↓
Fulfillment
     ↓
Delivery
     ↓
Completion
```

Depending on business rules, the system also handles:

* Cancellation
* Hold / review
* Order changes
* Adjustments
* Returns
* Refunds

### Outside the boundary

Examples:

* Customer's decision to purchase.
* Internal operation of a payment gateway.
* Internal operation of a delivery provider.
* General Ledger / accounting system.

---

# 7. Business Domains

The current Business Map consists of:

| Domain / Capability        | Responsibility                                            |
| -------------------------- | --------------------------------------------------------- |
| Customer                   | Customer information and identity                         |
| Product                    | Products and product information                          |
| Inventory                  | Stock availability and fulfillment-related stock handling |
| Order                      | Core order lifecycle and coordination                     |
| Payment                    | Payment lifecycle and payment-related state               |
| Delivery                   | Delivery lifecycle                                        |
| Order Change / Adjustment  | Controlled changes and post-order adjustments             |
| Return / Refund            | Returns and refund processes                              |
| User / Employee            | Internal system users                                     |
| Authorization / Permission | Access and business permissions                           |
| Audit / History            | Change and activity traceability                          |
| Notification               | Operational/customer notifications                        |
| Reporting / Operations     | Monitoring and operational reporting                      |

---

# 8. Domain Responsibility Principle

OrderFlow treats Order as the central business object, but Order should not own every business rule.

The domains should own rules related to their responsibilities.

For example:

```text
Order
 ├── Customer
 ├── Product
 ├── Inventory
 ├── Payment
 └── Delivery
```

Order coordinates the overall business flow while individual domains remain responsible for their own rules.

Examples:

* Inventory owns stock-related rules.
* Payment owns payment-related rules.
* Delivery owns delivery-related rules.
* Authorization owns access rules.
* Audit owns historical traceability.

This prevents the Order domain from becoming a "God Domain".

---

# 9. Order Lifecycle

The initial order lifecycle is:

```text
Order Request
      ↓
Draft
      ↓
Confirmed
      ↓
Preparing
      ↓
Shipped
      ↓
Delivered
      ↓
Completed
```

The lifecycle may also contain alternative states or flows such as:

* On Hold
* Cancelled
* Failed
* Change Requested

The exact state model will be refined during System Design.

---

# 10. Order Lifecycle Ownership

## 10.1 Draft

Sales owns the order.

Sales may correct relevant order information before confirmation.

---

## 10.2 Confirmed

Sales remains involved in the order.

Business approval may be required depending on the business rule.

---

## 10.3 Preparing

Warehouse owns the fulfillment process.

Warehouse may confirm fulfillment information but should not directly modify important transactional order information.

Problems should be reported to Sales or handled through an appropriate change process.

---

## 10.4 Shipped

Delivery staff owns the delivery process.

Delivery-related information and status may be updated according to delivery rules.

---

## 10.5 Delivered

The transaction has occurred.

Important transactional information should not be directly overwritten.

Administrative/contact information may be corrected under controlled rules.

---

## 10.6 Completed

The order is considered finalized.

Direct modification of the original transaction is not allowed.

Errors should be handled through an appropriate correction, adjustment, return, or refund process.

---

# 11. Order Creation Requirements

When an Order Request is recorded in OrderFlow, the system must validate the information required to create an Order.

Initial validation areas include:

* Customer validity
* Product validity
* Quantity validity
* Price validity
* Total amount validity
* Delivery information
* Payment/business policy
* Required order information
* Order source, where applicable

The exact validation rules will be refined during implementation design.

---

# 12. Invalid Order Requests

Not every validation failure must necessarily result in the same behavior.

Depending on the type of validation failure, the business may choose to:

### Option A — Reject creation

The Order is not created.

Example:

```text
Invalid Product
Invalid Quantity
Missing Required Information
```

### Option B — Create an Order requiring handling

The system may create an order in a special state when the business requires Sales or another actor to resolve the problem.

Example:

```text
Order created
     ↓
Requires Review
     ↓
Sales resolves problem
     ↓
Continue / Cancel
```

The exact classification of validation failures remains a business-policy decision.

---

# 13. Order Modification

Order information cannot always be freely modified.

The ability to modify an order depends on its lifecycle state and the type of information being changed.

For example:

```text
Draft
→ direct modification may be allowed

Preparing
→ important changes require a controlled process

Delivered
→ transactional information should not be directly modified

Completed
→ original transaction should remain immutable
```

---

# 14. Order Change Request

When an important order modification is requested after fulfillment has started, the system should use a controlled Change Request process.

Example:

```text
Original Quantity = 2

Sales requests:
Quantity = 5

        ↓

Change Request

        ↓

Warehouse checks stock

        ↓

Approve / Reject / Alternative handling
```

The original order should not simply be overwritten.

---

# 15. Change Request — Insufficient Inventory

If a requested quantity change cannot be fulfilled because of insufficient stock, possible business outcomes include:

### Option 1 — Reject Change

Keep the original quantity.

```text
Original Quantity = 2
Requested Quantity = 5

Result:
Change rejected
Order remains at quantity 2
```

### Option 2 — Wait for Stock

The request remains pending until enough inventory becomes available.

### Option 3 — Partial Fulfillment

If the business allows it:

```text
Required = 5
Available = 3

Possible:
3 now
2 later
```

The exact policy is TBD.

The customer may need to accept or reject the proposed handling depending on the business rule.

---

# 16. Post-Delivery Corrections

After delivery, the original transaction should be preserved.

If an error is discovered, the system should use a business process such as:

* Correction
* Adjustment
* Return
* Refund

The original transaction should not simply be overwritten.

---

# 17. Transactional Data Integrity

The following information is considered transactional and should not be directly modified after delivery:

* Product
* Quantity
* Unit Price
* Total Amount

These values contribute directly to the original transaction.

Administrative/contact information may have different rules.

Examples:

* Delivery Address
* Customer Phone

These may be corrected under controlled rules when doing so does not improperly alter the transaction.

---

# 18. Customer and Payment Information After Delivery

## Customer

Changing the customer after delivery may change the identity of the transaction.

Therefore:

```text
Customer A → Customer B
```

should not be treated as a normal direct edit.

A controlled correction process may be required.

---

## Payment Method

Payment method changes may affect financial reconciliation.

Therefore, changes after delivery should be governed by business rules rather than ordinary direct editing.

---

# 19. Price Correction / Adjustment

If an order was delivered with an incorrect price:

```text
Recorded Unit Price = 120,000 VND
Correct Unit Price = 100,000 VND
Quantity = 2

Original Amount = 240,000 VND
Correct Amount = 200,000 VND

Difference = 40,000 VND
```

The system should not overwrite the original transaction.

Instead:

```text
Original Transaction
        ↓
Correction / Price Adjustment Request
        ↓
Review / Approval
        ↓
Adjustment
        ↓
Refund if required
```

The final record should preserve:

* Original transaction value
* Corrected value
* Adjustment amount
* Refund amount, if applicable
* Relevant history

---

# 20. Payment Lifecycle

Payment is treated as a lifecycle independent from the Order lifecycle.

Possible payment states/concepts include:

* Pending
* Paid
* Failed
* Retried
* Partially Paid
* Payment Due
* Refund Required
* Refund Processing
* Refund Completed
* Disputed

The exact state model will be refined during System Design.

---

# 21. Order Lifecycle vs Payment Lifecycle

Order status and Payment status must not be treated as the same lifecycle.

For example:

```text
Order:
Preparing

Payment:
Paid
```

or:

```text
Order:
Preparing

Payment:
Pending
```

may both be valid depending on the payment policy.

---

# 22. Payment Policy

Whether an order can be confirmed before payment depends on the payment method/business policy.

### Prepaid

Example:

```text
Payment = Paid
       ↓
Order can be Confirmed
```

### Cash on Delivery

Example:

```text
Order can be Confirmed
       ↓
Payment = Pending / Payment Due
       ↓
Payment occurs during/after delivery
```

The project therefore separates:

* Payment Pending
* Payment Failed
* Payment Due

These concepts should not automatically be treated as equivalent.

---

# 23. Order Exceptions

Important exception scenarios identified during requirement analysis include:

* Customer cancellation
* Insufficient inventory
* Delivery failure
* Customer refusal of delivery
* Invalid order information
* Payment failure
* Fraud / manual review

Not every exception represents a final state.

For example:

```text
Payment Failed
    ↓
Retry
    ↓
Paid
```

or:

```text
Payment Failed
    ↓
Cancel Order
```

The final outcome depends on business rules.

---

# 24. Fraud / Manual Review

A suspected fraudulent order may be placed on hold while the result is investigated.

During review:

* The order may remain pending.
* Inventory may need to remain reserved depending on policy.
* The order may later continue or be cancelled.

The exact fraud-review policy is TBD.

---

# 25. Cancellation

Cancellation depends on the current order lifecycle state.

Examples:

```text
Before fulfillment
→ cancellation may be relatively straightforward

During fulfillment
→ cancellation may require coordination with Warehouse

During shipping
→ cancellation may require Delivery handling

After delivery
→ cancellation is generally replaced by a Return / Refund process
```

Exact cancellation rules by state remain to be defined.

---

# 26. Authorization and Business Permissions

Authorization is not limited to CRUD permissions.

The system should consider:

* Who can view an order.
* Who can create an order.
* Who can modify an order.
* Who can confirm an order.
* Who can approve changes.
* Who can cancel an order.
* Who can perform corrections.
* Who can access payment/financial information.

Permissions may depend on both:

```text
Actor / Role
+
Order Lifecycle State
+
Action
```

Example:

```text
Warehouse
+
Order = Preparing
+
Fulfillment action
→ Allowed

Warehouse
+
Order = Preparing
+
Change Quantity
→ Not directly allowed
```

---

# 27. Audit and History

Important business changes should be traceable.

The system should support determining:

* Who made the change.
* What was changed.
* Previous value where applicable.
* New value where applicable.
* When the change occurred.
* Relevant business action/request.

Audit history is especially important for:

* Order changes
* Price corrections
* Quantity changes
* Status changes
* Approvals
* Adjustments
* Refunds
* Permission-sensitive actions

---

# 28. Data Integrity Principles

OrderFlow should prioritize preservation of business transaction integrity.

Key principles:

1. Do not allow uncontrolled modification of important transactional data.
2. Do not overwrite historical transaction values when a correction is required.
3. Use controlled business processes for important changes.
4. Preserve relevant history.
5. Separate independent business lifecycles such as Order and Payment.
6. Prevent unauthorized users from performing business actions.
7. Handle concurrent changes carefully to avoid silent overwrites.

---

# 29. Concurrency / Data Conflict

The system must consider the possibility that multiple employees attempt to modify the same order.

Example:

```text
Sales A → Quantity = 5
Sales B → Quantity = 7
```

The system should not silently allow one update to overwrite the other without appropriate control.

The exact technical mechanism for handling concurrency will be determined during System Design.

---

# 30. Inventory Interaction

OrderFlow interacts with Inventory to determine whether required products and quantities can be fulfilled.

Important business situations include:

* Sufficient stock
* Insufficient stock
* Stock reservation
* Stock release
* Quantity changes
* Concurrent orders competing for the same stock

The exact inventory reservation/allocation model will be refined during System Design.

---

# 31. Delivery Interaction

Delivery is responsible for the delivery process after fulfillment.

Important situations include:

* Delivery preparation
* Shipment
* Successful delivery
* Delivery failure
* Customer refusal

OrderFlow manages its side of the delivery lifecycle but does not own the internal operations of an external delivery provider.

---

# 32. Functional Requirement Categories

The detailed functional requirements will be organized around the following capabilities:

### Customer

* Manage customer information.
* Validate customer information.
* Associate customers with orders.
* Handle controlled customer corrections.

### Product

* Manage products.
* Validate products used in orders.
* Associate products with order items.

### Inventory

* Check stock availability.
* Support fulfillment-related inventory operations.
* Handle insufficient stock.
* Support controlled stock changes related to order changes.

### Order

* Create orders.
* Validate orders.
* Confirm orders.
* Track lifecycle.
* Handle cancellation.
* Handle holds/review.
* Support controlled modification.
* Complete orders.

### Payment

* Track payment state.
* Support payment failure.
* Support retry.
* Support payment due/pending scenarios.
* Support refunds and adjustments where applicable.

### Delivery

* Manage delivery-related information.
* Track delivery state.
* Handle delivery failures.
* Record successful delivery.

### Change / Adjustment

* Create change requests.
* Review requests.
* Approve/reject requests.
* Record adjustments.
* Preserve original transaction information.

### Return / Refund

* Handle returns.
* Determine refund requirements.
* Track refund processing.
* Preserve refund history.

### Authorization

* Control access.
* Control business actions based on role and lifecycle state.

### Audit

* Record important business changes.
* Preserve relevant historical information.

### Notification

* Support operational/customer notifications where required.

### Reporting

* Provide operational visibility and reporting.

---

# 33. Non-Functional Requirement Direction

The following NFR areas have been identified but will be specified in detail during later phases.

## 33.1 Security

The system should protect:

* Customer information
* Order information
* Payment-related information
* Business operations

It should enforce authentication and authorization.

---

## 33.2 Data Integrity

The system should prevent:

* Invalid transactional data
* Unauthorized modification
* Silent overwriting of important data
* Loss of transaction history

---

## 33.3 Auditability

Important business operations should be traceable.

---

## 33.4 Reliability

Important order operations should not result in inconsistent business state.

---

## 33.5 Performance

The system should support normal operational order-processing workloads with acceptable response times.

Specific targets will be defined later.

---

## 33.6 Maintainability

The system should separate business responsibilities by domain/capability and avoid excessive coupling.

---

# 34. External Dependencies

Potential external systems include:

* Payment Gateway
* Delivery Provider
* Accounting System

OrderFlow does not implement the internal business processes of these systems.

Integration details will be defined in later system-design phases.

---

# 35. Business Policy / TBD Items

The following decisions are intentionally not finalized in SRS v1:

* Exact order states.
* Exact payment states.
* Exact delivery states.
* Exact inventory reservation behavior.
* Whether partial fulfillment is allowed.
* Whether backorders are allowed.
* Exact cancellation rules for each lifecycle state.
* Exact refund rules.
* Exact approval requirements.
* Exact fraud-review behavior.
* Exact validation behavior for each invalid order condition.
* Exact field-level modification permissions.
* Exact concurrency-control mechanism.
* Exact external integration mechanism.
* Exact notification rules.
* Exact reporting requirements.
* Exact performance targets.

These items should be resolved when they become necessary during System Design, Database Design, API Design, Implementation, or Testing.

---

# 36. Assumptions

Current assumptions:

1. OrderFlow begins when an Order Request enters the system.
2. Customer purchase decisions are outside the system boundary.
3. Order is the central business process but does not own every domain rule.
4. Payment has a lifecycle independent from Order.
5. Delivery has a lifecycle independent from Order.
6. Important transactional information should be preserved after delivery.
7. Important corrections should use controlled business processes.
8. External payment and delivery providers are outside the system boundary.
9. The project will prioritize realistic business rules over implementing every possible enterprise feature.
10. Requirements may be refined as the system is designed and implemented.

---

# 37. Requirement Evolution

SRS v1 is a baseline rather than a claim that every possible business scenario has been discovered.

When a new requirement or business rule is discovered during development:

```text
New scenario discovered
        ↓
Analyze business impact
        ↓
Determine whether it is:
    - Requirement
    - Business Rule
    - Technical Decision
    - Out of Scope
        ↓
Update documentation
        ↓
Update affected design/code/tests
```

Requirement changes should be recorded rather than silently changing system behavior.

---

# 38. Requirement Analysis Completion Criteria

Phase 1 is considered sufficiently complete when the team can answer:

* Why does OrderFlow exist?
* Who uses it?
* What business problem does it solve?
* What is inside and outside the system boundary?
* What are the major business domains?
* What is the core order lifecycle?
* Who owns actions at each stage?
* Which data must be protected from direct modification?
* How are important changes handled?
* How are payment and order lifecycles separated?
* What major exceptions exist?
* Which decisions remain business-policy/TBD?
* Which capabilities are explicitly out of scope?

OrderFlow currently satisfies this baseline.

---

# 39. Next Phase

With SRS v1 established, the project can proceed to:

```text
PHASE 2 — SYSTEM DESIGN

        ↓

Architecture
Domain Boundaries
Application Structure
Component Responsibilities
Order State Model
Payment State Model
Concurrency Strategy
Authorization Model
Integration Boundaries

        ↓

PHASE 3 — DATABASE DESIGN
```

The design phase may reveal missing requirements. Such requirements should be added through controlled updates to this SRS rather than attempting to predict every possible scenario before implementation.

---

# 40. Version History

| Version | Status   | Description                                         |
| ------- | -------- | --------------------------------------------------- |
| 1.0     | Baseline | Initial SRS derived from requirement discovery      |
| 1.x     | Future   | Refinements discovered during design/implementation |
| 2.0     | Future   | Major scope or business-process changes             |
