"""
This Python code demonstrates Schema-Guided Reasoning (SGR) with OpenAI. It:

- implements a business agent capable of planning and reasoning
- implements tool calling using only SGR and simple dispatch
- uses with a simple (inexpensive) non-reasoning model for that

To give this agent something to work with, we ask it to help with running
a small business - selling courses to help to achieve AGI faster.

Once this script starts, it will emulate in-memory CRM with invoices,
emails, products and rules. Then it will execute sequentially a set of
tasks (see TASKS below). In order to carry them out, Agent will have to use
tools to issue invoices, create rules, send emails, and a few others.

Read more about SGR: http://abdullin.com/schema-guided-reasoning/

This demo is described in more detail here: https://abdullin.com/schema-guided-reasoning/demo
"""


# Let's start by implementing our customer management system. For the sake of
# simplicity it will live in memory and have a very simple DB structure


DB = {
    "rules": [],
    "invoices": {},
    "emails": [],
    "products": {
        "SKU-205": { "name":"AGI 101 Course Personal", "price":258},
        "SKU-210": { "name": "AGI 101 Course Team (5 seats)", "price":1290},
        "SKU-220": { "name": "Building AGI - online exercises", "price":315},
    },
}

# Now, let's define a few tools which could be used by LLM to do something 
# useful with this customer management system. We need tools to issue invoices, 
# send emails, create rules and memorize new rules. Maybe a tool to cancel invoices.

from typing import List, Union, Literal, Annotated
from annotated_types import MaxLen, Le, MinLen
from pydantic import BaseModel, Field


# Tool: Sends an email with subject, message, attachments to a recipient
class SendEmail(BaseModel):
    tool: Literal["send_email"]
    subject: str
    message: str
    files: List[str]
    recipient_email: str

# Tool: Retrieves customer data such as rules, invoices, and emails from the database
class GetCustomerData(BaseModel):
    tool: Literal["get_customer_data"]
    email: str

# Tool: Issues an invoice to a customer, allowing up to a 50% discount
class IssueInvoice(BaseModel):
    tool: Literal["issue_invoice"]
    email: str
    skus: List[str]
    discount_percent: Annotated[int, Le(50)] # never more than 50% discount

# Tool: Cancels (voids) an existing invoice and records the reason
class VoidInvoice(BaseModel):
    tool: Literal["void_invoice"]
    invoice_id: str
    reason: str

# Tool: Saves a custom rule for interacting with a specific customer
class CreateRule(BaseModel):
    tool: Literal["remember"]
    email: str
    rule: str


# This function handles executing commands issued by the agent. It simulates
# operations like sending emails, managing invoices, and updating customer
# rules within the in-memory database.
def dispatch(cmd: BaseModel):
    # here is how we can simulate email sending
    # just append to the DB (for future reading), return composed email
    # and pretend that we sent something
    if isinstance(cmd, SendEmail):
        email = {
            "to": cmd.recipient_email,
            "subject": cmd.subject,
            "message": cmd.message,
        }
        DB["emails"].append(email)
        return email


    # likewize rule creation just stores rule associated with customer
    if isinstance(cmd, CreateRule):
        rule = {
            "email": cmd.email,
            "rule": cmd.rule,
        }
        DB["rules"].append(rule)
        return rule

    # customer data reading - doesn't change anything. It queries DB for all
    # records associated with the customer
    if isinstance(cmd, GetCustomerData):
        addr = cmd.email
        return {
            "rules": [r for r in DB["rules"] if r["email"] == addr],
            "invoices": [t for t in DB["invoices"].items() if t[1]["email"] == addr],
            "emails": [e for e in DB["emails"] if e.get("to") == addr],
        }

    # invoice generation is going to be more tricky
    # it will demonstrate discount calculation (we know that LLMs shouldn't be trusted
    # with math. It also shows how to report problems back to LLM.
    # ultimately, it computes a new invoice number and stores it in the DB
    if isinstance(cmd, IssueInvoice):
        total = 0.0
        for sku in cmd.skus:
            product = DB["products"].get(sku)
            if not product:
                return f"Product {sku} not found"

            total += product["price"]

        discount = round(total * 1.0 * cmd.discount_percent / 100.0, 2)

        invoice_id = f"INV-{len(DB['invoices']) + 1}"

        invoice = {
            "id": invoice_id,
            "email": cmd.email,
            "file": "/invoices/" + invoice_id + ".pdf",
            "skus": cmd.skus,
            "discount_amount": discount,
            "discount_percent": cmd.discount_percent,
            "total": total,
            "void": False,
        }
        DB["invoices"][invoice_id] = invoice
        return invoice


    # invoice cancellation marks a specific invoice as void
    if isinstance(cmd, VoidInvoice):
        invoice = DB["invoices"].get(cmd.invoice_id)
        if not invoice:
            return f"Invoice {cmd.invoice_id} not found"
        invoice["void"] = True
        return invoice


# Now, having such DB and tools, we could come up with a list of tasks
# that we can carry out sequentially
TASKS = [
    # 1. this one should create a new rule for sama
    "Rule: address sama@openai.com as 'The SAMA', always give him 5% discount.",
    # 2. this should create a rule for elon
    "Rule for elon@x.com: Email his invoices to finance@x.com",
    # 3. now, this task should create an invoice for sama that includes one of each
    # product. But it should also remember to give discount and address him
    # properly
    "sama@openai.com wants one of each product. Email him the invoice",
    # 4. Even more tricky - we need to create the invoice for Musk based on the
    # invoice of sama, but twice. Plus LLM needs to remeber to use the proper
    # email address for invoices - finance@x.com
    "elon@x.com wants 2x of what sama@openai.com got. Send invoice",
    # 5. even more tricky. Need to cancel old invoice (we never told LLMs how)
    # and issue the new invoice. BUT it should pull the discount from sama and
    # triple it. Obviously the model should also remember to send invoice
    # not to elon@x.com but to finance@x.com
    "redo last elon@x.com invoice: use 3x discount of sama@openai.com",
]

# let's define one more special command. LLM can use it whenever
# it thinks that its task is completed. It will report results with that.
class ReportTaskCompletion(BaseModel):
    tool: Literal["report_completion"]
    completed_steps_laconic: List[str]
    code: Literal["completed", "failed"]

# now we have all sub-schemas in place, let's define SGR schema for the agent
class NextStep(BaseModel):
    # we'll give some thinking space here
    current_state: str
    # Cycle to think about what remains to be done. at least 1 at most 5 steps
    # we'll use only the first step, discarding all the rest.
    plan_remaining_steps_brief: Annotated[List[str], MinLen(1), MaxLen(5)]
    # now let's continue the cascade and check with LLM if the task is done
    task_completed: bool
    # Routing to one of the tools to execute the first remaining step
    # if task is completed, model will pick ReportTaskCompletion
    function: Union[
        ReportTaskCompletion,
        SendEmail,
        GetCustomerData,
        IssueInvoice,
        VoidInvoice,
        CreateRule,
    ] = Field(..., description="execute first remaining step")

# here is the prompt with some core context
# since the list of products is small, we can merge it with prompt
# In a bigger system, could add a tool to load things conditionally
system_prompt = f"""
You are a business assistant helping Rinat Abdullin with customer interactions.

- Clearly report when tasks are done.
- Always send customers emails after issuing invoices (with invoice attached).
- Be laconic. Especially in emails
- No need to wait for payment confirmation before proceeding.
- Always check customer data before issuing invoices or making changes.

Products: {DB["products"]}""".strip()

# now we just need to implement the method to bring that all together
# we will use rich for pretty printing in console

import json
from openai import OpenAI
from rich.console import Console
from rich.panel import Panel
from rich.rule import Rule

client = OpenAI()
console = Console()
print = console.print

# Runs each defined task sequentially. The AI agent uses reasoning to determine
# what steps are required to complete each task, executing tools as needed.
def execute_tasks():

    # we'll execute all tasks sequentially. You can add your tasks
    # of prompt user to write their own
    for task in TASKS:
        print("\n\n")
        print(Panel(task, title="Launch agent with task", title_align="left"))

        # log will contain conversation context for the agent within task
        log = [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": task}
        ]

        # let's limit number of reasoning steps by 20, just to be safe
        for i in range(20):
            step = f"step_{i+1}"
            print(f"Planning {step}... ", end="")

            # This sample relies on OpenAI API. We specifically use 4o, since
            # GPT-5 has bugs with constrained decoding as of August 14, 2025
            completion = client.beta.chat.completions.parse(
                model="gpt-4o",
                response_format=NextStep,
                messages=log,
                max_completion_tokens=10000,
            )
            job = completion.choices[0].message.parsed

            # if SGR decided to finish, let's complete the task
            # and quit this loop
            if isinstance(job.function, ReportTaskCompletion):
                print(f"[blue]agent {job.function.code}[/blue].")
                print(Rule("Summary"))
                for s in job.function.completed_steps_laconic:
                    print(f"- {s}")
                print(Rule())
                break

            # let's be nice and print the next remaining step (discard all others)
            print(job.plan_remaining_steps_brief[0], f"\n  {job.function}")

            # Let's add tool request to conversation history as if OpenAI asked for it.
            # a shorter way would be to just append `job.model_dump_json()` entirely
            log.append({
                "role": "assistant",
                "content": job.plan_remaining_steps_brief[0],
                "tool_calls": [{
                    "type": "function",
                    "id": step,
                    "function": {
                        "name": job.function.tool,
                        "arguments": job.function.model_dump_json(),
                }}]
            })

            # now execute the tool by dispatching command to our handler
            result = dispatch(job.function)
            txt = result if isinstance(result, str) else json.dumps(result)
            #print("OUTPUT", result)
            # and now we add results back to the convesation history, so that agent
            # we'll be able to act on the results in the next reasoning step.
            log.append({"role": "tool", "content": txt, "tool_call_id": step})

if __name__ == "__main__":
    execute_tasks()