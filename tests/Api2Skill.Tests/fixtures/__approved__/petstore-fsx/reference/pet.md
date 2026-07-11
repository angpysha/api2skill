# pet

Everything about your Pets

## getPetById

`GET /pet/{petId}`

Find pet by ID

Returns a single pet.

**Parameters**

| name | in | required | type | description |
|---|---|---|---|---|
| `petId` | Path | yes | Integer | ID of pet to return |

**Auth**: `api_key`

**Responses**

- `200`: successful operation
- `404`: Pet not found

---

## addPet

`POST /pet`

Add a new pet to the store

**Request body**

- Content-Type: `application/json` (required)
- Shape: object { name, status }

**Auth**: `petstore_auth`

**Responses**

- `200`: successful operation

---

