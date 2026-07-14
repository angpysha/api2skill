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

**Request body**: none

**Auth**: `api_key`

**Responses**

### `200`: successful operation

- Content-Type: `application/json`
- Shape: object { id, name, status }

| property | type | required | description |
|---|---|---|---|
| `id` | Integer (int64) | yes | Pet id |
| `name` | String | yes | Pet name |
| `status` | String | no | Pet status in the store |

Example:

```json
{ "id": 0, "name": "string", "status": "string" }
```

### `404`: Pet not found

- Body: none documented in the OpenAPI response

---

## addPet

`POST /pet`

Add a new pet to the store

**Request body**

- Content-Type: `application/json` (required)
- Shape: object { name, status }

| property | type | required | description |
|---|---|---|---|
| `name` | String | yes | Pet name |
| `status` | String | no | Pet status in the store |

Example:

```json
{ "name": "string", "status": "string" }
```

**Auth**: `petstore_auth`

**Responses**

### `200`: successful operation

- Content-Type: `application/json`
- Shape: object { id, name, status }

| property | type | required | description |
|---|---|---|---|
| `id` | Integer (int64) | yes | Pet id |
| `name` | String | yes | Pet name |
| `status` | String | no | Pet status in the store |

Example:

```json
{ "id": 0, "name": "string", "status": "string" }
```

---

